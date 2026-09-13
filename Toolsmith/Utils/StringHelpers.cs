using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Toolsmith.Utils {
    public static class StringHelpers {

        //The tooltip palette. Taken from the colors the base game already uses in its own lang strings so a
        //Toolsmith tooltip reads as part of the game rather than as something bolted on: #84ff84/#ff8484 are
        //vanilla's own green and red, #fff785 its yellow. Purple has no vanilla precedent and marks the one state
        //above "good" - a part that has taken no damage at all, or a head that cannot be honed any further.
        public const string PristineColor = "#c9a0ff";
        public const string GoodColor = "#84ff84";
        public const string WornColor = "#fff785";
        public const string PoorColor = "#ff8484";
        public const string UnknownColor = "#bbbbbb";

        //One owner for the percentage-to-color mapping, so a head, a handle, a binding and a sharpness bar all
        //band at the same thresholds. Returning the color rather than the finished string keeps the wording in the
        //lang file where a translator can reach it.
        public static string ColorForRemainingPercent(float remainingPercent)
        {
            return remainingPercent switch
            {
                >= 1.0f => PristineColor,
                > 0.66f => GoodColor,
                > 0.33f => WornColor,
                _ => PoorColor
            };
        }

        //The durability lines carry a current and a max rather than a percent, and a max of zero would otherwise
        //divide by zero on a part whose stats have not been written yet.
        public static string ColorForDurability(int current, int max)
        {
            return max <= 0 ? UnknownColor : ColorForRemainingPercent((float)current / (float)max);
        }

        //The stat lines on a part - multipliers and bonuses - are not a fraction of anything, so they band by sign
        //rather than by threshold: a bonus helps, a malus hurts, and zero is worth neither color.
        public static string ColorForBonus(double bonus)
        {
            return bonus switch
            {
                > 0 => GoodColor,
                < 0 => PoorColor,
                _ => UnknownColor
            };
        }

        //A multiplier is measured against 1.0 rather than against zero, so it needs its own comparison even though
        //it bands to the same three colors.
        public static string ColorForMultiplier(double multiplier)
        {
            return multiplier switch
            {
                > 1.0 => GoodColor,
                < 1.0 => PoorColor,
                _ => UnknownColor
            };
        }

        //Bands a mining speed. 1x is the bare-hands baseline that vanilla itself filters out, so anything actually
        //shown is at least some improvement - the colors separate "worth using this tool" from "barely faster".
        public static string ColorForMiningSpeed(float speed)
        {
            return speed switch
            {
                >= 5.0f => PristineColor,
                >= 3.0f => GoodColor,
                >= 1.5f => WornColor,
                _ => UnknownColor
            };
        }

        //Deletes the whole line that begins with the given text, including its trailing newline. Vanilla writes
        //several tooltip lines this mod renders itself instead, and each has to come back out of the buffer before
        //the replacement goes in. The prefix is looked up in the same translation table vanilla wrote the line from,
        //so this keeps working in any language.
        //
        //Does nothing when the prefix is absent, so a tool that never had the line costs one scan and no special
        //case at the call site.
        public static void RemoveTooltipLineStartingWith(StringBuilder tooltip, string linePrefix) {
            if (linePrefix == null || linePrefix.Length == 0) {
                return;
            }

            string text = tooltip.ToString();
            int lineStart = text.IndexOf(linePrefix, StringComparison.Ordinal);
            if (lineStart < 0) {
                return;
            }

            //Walk back to the start of the line so the label's own leading text goes with it, and forward past the
            //newline so removing a line does not leave a blank one behind.
            while (lineStart > 0 && text[lineStart - 1] != '\n') {
                lineStart--;
            }
            int lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0) {
                lineEnd = text.Length - 1;
            }

            tooltip.Remove(lineStart, lineEnd - lineStart + 1);
        }

        //Pass this the starting and ending index values you want it to set to the proper location in the tooltip where the Vanilla Durability Line is located.
        //Can technically start partway through the tooltip if needed by setting them to a value ahead of time, but general use is with them starting at 0 to start at the top of the tooltip.
        public static void FindTooltipVanillaDurabilityLine(ref int startIndex, ref int endIndex, StringBuilder tooltip, IWorldAccessor world, bool withDebugInfo) {
            bool debugFlag = false; //True after the Attribute line has been found
            bool foundLine = false;
            while (endIndex < tooltip.Length && foundLine == false) { //Find and trim off the original 'Durability' information, and then...
                if (tooltip[endIndex] == '\n') {
                    startIndex = endIndex + 1;
                }
                if (!withDebugInfo && tooltip[endIndex] == 'D') { //Finds the line by its English initial, so this does not locate the line in other languages.
                    foundLine = true;
                }
                if (withDebugInfo && debugFlag && tooltip[endIndex] == 'D') { //This whole bit is specifically searching for the English translated code... So this might cause issues in other languages. Oof.
                    if (startIndex == endIndex) {
                        foundLine = true;
                    }
                }
                if (withDebugInfo && !debugFlag && (((world.Api.Side == EnumAppSide.Client && (world.Api as ICoreClientAPI).Input.KeyboardKeyStateRaw[1]) && tooltip[endIndex] == 'A') || (tooltip[endIndex] == 'C'))) {
                    startIndex = endIndex;
                    debugFlag = true;
                }
                endIndex++;
            }
            while (endIndex < tooltip.Length && tooltip[endIndex] != '\n') {
                endIndex++;
            }
        }
    }
}
