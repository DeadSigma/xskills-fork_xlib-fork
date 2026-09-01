using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Vintagestory.Common;

namespace xSkillGilded {
    public class VTMLblock {
        public string text;
        public string[] words;
        public Vector4 color = new(1,1,1,1);
        public bool bold;
        public bool italic;
        public bool lineBreak = false;

        public VTMLblock(string text) {
            this.text = text;
            words = text.Split(' ');

            // resourceLoader.api.Logger.Debug($"   > create block {text}");
        }
    }

    public static class VTML {
        public static List<VTMLblock> parseVTML(string _str) {
            // resourceLoader.api.Logger.Debug($"Parsing {_str}");

            var blocks  = new List<VTMLblock>();
            int pos     = 0;
            var regex   = new Regex(@"(<(i|strong|font)([^>]*)>(.*?)<\/\2>)|(<br\s*\/?>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var matches = regex.Matches(_str);

            foreach (Match match in matches) {
                if (match.Index > pos) { // capture before first '<'
                    string plain = _str.Substring(pos, match.Index - pos);
                    if (!string.IsNullOrWhiteSpace(plain)) {
                        blocks.Add(new VTMLblock(plain.Trim()));
                    }
                }

                string tag = match.Groups[2].Value.ToLower();
                string innerText = match.Groups[4].Value;
                var block = new VTMLblock(innerText);

                if(!string.IsNullOrWhiteSpace(match.Groups[5].Value)) {  
                    block.lineBreak = true;
                    blocks.Add(block);

                } else {
                    if (tag == "i") block.italic = true;
                    if (tag == "strong") block.bold = true;
                    if (tag == "font") {
                        var attr = match.Groups[3].Value;
                        var colorMatch = Regex.Match(attr, @"color\s*=\s*""(#?[0-9a-fA-F]{6,8})""");
                        var opacityMatch = Regex.Match(attr, @"opacity\s*=\s*""([\d\.]+)""");

                        if (colorMatch.Success) {
                            var hex = colorMatch.Groups[1].Value.Replace("#", "");
                            if (hex.Length == 6) {
                                var r = Convert.ToInt32(hex.Substring(0,2), 16) / 255f;
                                var g = Convert.ToInt32(hex.Substring(2,2), 16) / 255f;
                                var b = Convert.ToInt32(hex.Substring(4,2), 16) / 255f;
                                block.color = new Vector4(r, g, b, 1);
                            }
                        
                            // resourceLoader.api.Logger.Debug($"   > set color {block.color}");
                        }
                        if (opacityMatch.Success) {
                            if (float.TryParse(opacityMatch.Groups[1].Value, out float alpha)) {
                                block.color.W = alpha;
                            }
                        }
                    }

                    blocks.Add(block);
                }

                pos = match.Index + match.Length;
            }

            if (pos < _str.Length) {
                string plain = _str.Substring(pos);
                if (!string.IsNullOrWhiteSpace(plain)) {
                    blocks.Add(new VTMLblock(plain.Trim()));
                }
            }

            return blocks;
        }

        public static string WrapFont(string str, string color) {
            return $"<font color=\"{color}\">{str}</font>";
        }
    }
}
