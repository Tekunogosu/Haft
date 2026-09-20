using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace CakeBuild
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            return new CakeHost()
                .UseContext<BuildContext>()
                .Run(args);
        }
    }

    public class BuildContext : FrostingContext
    {
        public const string ProjectName = "Haft";
        public string BuildConfiguration { get; }
        public string Version { get; }
        public string Name { get; }
        public bool SkipJsonValidation { get; }

        public BuildContext(ICakeContext context)
            : base(context)
        {
            BuildConfiguration = context.Argument("configuration", "Release");
            SkipJsonValidation = context.Argument("skipJsonValidation", false);
            var modInfo = context.DeserializeJsonFromFile<ModInfo>($"../{ProjectName}/modinfo.json");
            Version = modInfo.Version;
            Name = modInfo.ModID;
        }
    }

    [TaskName("ValidateJson")]
    public sealed class ValidateJsonTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            if (context.SkipJsonValidation)
            {
                return;
            }
            var jsonFiles = context.GetFiles($"../{BuildContext.ProjectName}/assets/**/*.json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file.FullPath);
                    JToken.Parse(json);
                }
                catch (JsonException ex)
                {
                    throw new Exception($"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
                }
            }
        }
    }

    [TaskName("Build")]
    [IsDependentOn(typeof(ValidateJsonTask))]
    public sealed class BuildTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            context.DotNetClean($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
                new DotNetCleanSettings
                {
                    Configuration = context.BuildConfiguration
                });


            context.DotNetPublish($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
                new DotNetPublishSettings
                {
                    Configuration = context.BuildConfiguration
                });
        }
    }

    [TaskName("Package")]
    [IsDependentOn(typeof(BuildTask))]
    public sealed class PackageTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
            //Only the staging folder is cleaned, not Releases itself. Cleaning Releases deleted every
            //previously packaged zip, so a build could not be rolled back to and an overwritten install
            //had nothing to restore from. The staging folder still has to be emptied, or a file dropped
            //by an earlier build - an asset since renamed or removed - would be packaged into this zip.
            context.EnsureDirectoryExists("../Releases");
            context.EnsureDirectoryExists($"../Releases/{context.Name}");
            context.CleanDirectory($"../Releases/{context.Name}");
            context.CopyFiles($"../{BuildContext.ProjectName}/bin/{context.BuildConfiguration}/Mods/mod/publish/*", $"../Releases/{context.Name}");
            if (context.DirectoryExists($"../{BuildContext.ProjectName}/assets"))
            {
                context.CopyDirectory($"../{BuildContext.ProjectName}/assets", $"../Releases/{context.Name}/assets");
            }
            context.CopyFile($"../{BuildContext.ProjectName}/modinfo.json", $"../Releases/{context.Name}/modinfo.json");
            if (context.FileExists($"../{BuildContext.ProjectName}/modicon.png"))
            {
                context.CopyFile($"../{BuildContext.ProjectName}/modicon.png", $"../Releases/{context.Name}/modicon.png");
            }
            WriteDeterministicZip($"../Releases/{context.Name}", $"../Releases/{context.Name}_{context.Version}.zip");
        }

        // Cake's context.Zip takes entry order from the filesystem and mtimes from
        // the files, so two builds of identical source produce zips with different
        // bytes. That defeats comparing an installed copy against the packaged one
        // by hash: the hash moves whenever the mod is rebuilt, whether or not
        // anything changed, so real drift and a harmless rebuild look the same.
        //
        // Sorting the entries and stamping one fixed timestamp makes the package a
        // function of its contents alone.
        private static void WriteDeterministicZip(string sourceDirectory, string zipPath)
        {
            var root = Path.GetFullPath(sourceDirectory);

            var entries = new List<string>();

            // Directory entries are written as well as files. They carry no data,
            // but every mod the testbed loads has them, so a package without them
            // would be the only one of its kind -- not something to find out from
            // a mod that silently fails to load.
            foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                entries.Add(ToEntryName(root, dir) + "/");
            }

            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                entries.Add(ToEntryName(root, file));
            }

            // Ordinal, not culture-aware: a culture-sensitive sort would order
            // entries differently on a machine with another locale, which is the
            // same non-determinism one level up.
            entries.Sort(StringComparer.Ordinal);

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            using var stream = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            foreach (var name in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);

                // Any fixed value works; this is the DOS epoch the zip format
                // stores, and the earliest a ZipArchiveEntry accepts.
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

                if (name.EndsWith('/'))
                {
                    continue;
                }

                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(Path.Combine(root, name));
                fileStream.CopyTo(entryStream);
            }
        }

        private static string ToEntryName(string root, string path)
        {
            return Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
        }
    }

    [TaskName("Default")]
    [IsDependentOn(typeof(PackageTask))]
    public class DefaultTask : FrostingTask
    {
    }
}