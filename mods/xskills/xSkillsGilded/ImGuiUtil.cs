using Cairo;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace xSkillGilded {
    enum HALIGN {
        Left,
        Center,
        Right
    }

    enum VALIGN {
        Top,
        Center,
        Bottom
    }

    internal static class ImGuiUtil {
        public const float baseUiScale = 1.125f;
        public static float uiScale = 1.125f;

        public static Font fTitle, fTitleGold;
        public static Font fSubtitle, fSubtitleGold;

        public static float windowPosX;
        public static float windowPosY;
        public static Vector4 drawCurrentColor = Vector4.One;

        public static bool useInternalTextDrawer = false;
        
        public static Vector4 c_white  = new(1);
        public static Vector4 c_dkgrey = hexToVec4("392a1c");
        public static Vector4 c_grey   = hexToVec4("92806a");
        public static Vector4 c_lime   = hexToVec4("7ac62f");
        public static Vector4 c_red    = hexToVec4("bf663f");
        public static Vector4 c_gold   = hexToVec4("feae34");

        public static void drawSetColor(Vector4 c, float alpha = -1f) { 
            if(alpha > -1) c = new(c.X, c.Y, c.Z, alpha);
            drawCurrentColor = c; 
        }

        #region Text
        public static Vector2 drawText(string text, float x, float y, HALIGN ha = HALIGN.Left, VALIGN va = VALIGN.Top) {
            Vector2 textSize   = ImGui.CalcTextSize(text);
            float textWidth  = textSize.X;
            float textHeight = textSize.Y;

            float _x = x;
            float _y = y;

            switch(ha) {
                case HALIGN.Left:   _x = x;                 break;
                case HALIGN.Center: _x = x - textWidth / 2; break;
                case HALIGN.Right:  _x = x - textWidth;     break;
            }

            switch(va) {
                case VALIGN.Top:    _y = y;                  break;
                case VALIGN.Center: _y = y - textHeight / 2; break;
                case VALIGN.Bottom: _y = y - textHeight;     break;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, drawCurrentColor);
            ImGui.SetCursorPos(new(_x, _y));
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();

            return textSize;
        }

        public static Vector2 drawTextFont(Font font, string text, float x, float y, HALIGN ha = HALIGN.Left, VALIGN va = VALIGN.Top) {
            if(useInternalTextDrawer) {
                if(font.hasFallbackColor) drawSetColor(font.fallbackColor);
                Vector2 size = drawText(text, x, y, ha, va);
                if(font.hasFallbackColor) drawSetColor(Vector4.One);
                return size;
            }

            Vector2 textSize = font.CalcTextSize(text);
            
            float textWidth  = textSize.X;
            float textHeight = textSize.Y;

            float _x = x;
            float _y = y;

            switch(ha) {
                case HALIGN.Left:   _x = x;                 break;
                case HALIGN.Center: _x = x - textWidth / 2; break;
                case HALIGN.Right:  _x = x - textWidth;     break;
            }

            switch(va) {
                case VALIGN.Top:    _y = y;                  break;
                case VALIGN.Center: _y = y - textHeight / 2; break;
                case VALIGN.Bottom: _y = y - textHeight;     break;
            }

            foreach(char c in text) {
                ImGui.SetCursorPos(new ( _x, _y ));
                float w = font.drawCharColor(c, drawCurrentColor);
                _x += w;
            }

            return textSize;
        }

        public static float drawTextWrap(string text, float x, float y, HALIGN ha = HALIGN.Left, VALIGN va = VALIGN.Top, float wrapWidth = 99999) {
            string[] words = text.Split(' ');
            List<string> lines = new List<string>();

            string currentLine = "";
            foreach (string word in words) {
                if (ImGui.CalcTextSize(currentLine + word).X > wrapWidth) {
                    lines.Add(currentLine);
                    currentLine = word + " ";
                } else
                    currentLine += word + " ";
            }

            if (currentLine.Length > 0) 
                lines.Add(currentLine);

            float totalWidth  = 0;
            float totalHeight = lines.Count * ImGui.GetTextLineHeight();

            foreach (string line in lines) {
                float lineWidth = ImGui.CalcTextSize(line).X;
                if (lineWidth > totalWidth) 
                    totalWidth = lineWidth;
            }

            float _y = y;
            switch(va) {
                case VALIGN.Top:    _y = y; break;
                case VALIGN.Center: _y = y - totalHeight / 2; break;
                case VALIGN.Bottom: _y = y - totalHeight;     break;
            }

            ImGui.PushStyleColor(ImGuiCol.Text, drawCurrentColor);
            foreach(string line in lines) {
                float _x = x;
                switch(ha) {
                    case HALIGN.Left:   _x = x; break;
                    case HALIGN.Center: _x = x - ImGui.CalcTextSize(line).X / 2; break;
                    case HALIGN.Right:  _x = x - ImGui.CalcTextSize(line).X;     break;
                }

                ImGui.SetCursorPos(new(_x, _y));
                ImGui.TextUnformatted(line);
                _y += ImGui.GetTextLineHeight();
            }
            ImGui.PopStyleColor();

            return totalHeight;
        }
        
        public static float drawTextFontWrap(Font font, string text, float x, float y, HALIGN ha = HALIGN.Left, VALIGN va = VALIGN.Top, float wrapWidth = 99999) {
            if(useInternalTextDrawer) {
                if(font.hasFallbackColor) drawSetColor(font.fallbackColor);
                float size = drawTextWrap(text, x, y, ha, va, wrapWidth);
                if(font.hasFallbackColor) drawSetColor(Vector4.One);
                return size;
            }

            string[] words = text.Split(' ');
            List<string> lines = new List<string>();

            string currentLine = "";
            foreach (string word in words) {
                if (font.CalcTextSize(currentLine + word).X > wrapWidth) {
                    lines.Add(currentLine.Trim());
                    currentLine = word + " ";
                } else
                    currentLine += word + " ";
            }

            if (currentLine.Length > 0) 
                lines.Add(currentLine.Trim());

            float totalWidth  = 0;
            float totalHeight = lines.Count * font.getLineHeight();

            foreach (string line in lines) {
                float lineWidth = font.CalcTextSize(line).X;
                if (lineWidth > totalWidth) 
                    totalWidth = lineWidth;
            }

            float _y = y;
            switch(va) {
                case VALIGN.Top:    _y = y; break;
                case VALIGN.Center: _y = y - totalHeight / 2; break;
                case VALIGN.Bottom: _y = y - totalHeight;     break;
            }

            foreach(string line in lines) {
                float _x = x;
                switch(ha) {
                    case HALIGN.Left:   _x = x; break;
                    case HALIGN.Center: _x = x - font.CalcTextSize(line).X / 2; break;
                    case HALIGN.Right:  _x = x - font.CalcTextSize(line).X;     break;
                }

                ImGui.SetCursorPos(new (_x, _y));
                foreach(char c in line) {
                    ImGui.SetCursorPos(new ( _x, _y ));
                    float w = font.drawCharColor(c, drawCurrentColor);
                    _x += w;
                }
                _y += font.getLineHeight();
            }

            return totalHeight;
        }

        public static float drawTextVTML(List<VTMLblock> blocks, float x, float y, float wrapWidth = 99999) {
            float height     = 0;
            float currLine   = 0;
            float lineHeight = ImGui.GetTextLineHeight();

            float _x = x;
            float _y = y;

            foreach(VTMLblock block in blocks) {
                if(block.lineBreak) {
                    currLine = 0;
                    _x =  x;
                    _y += lineHeight;
                    height += lineHeight;
                    continue;
                }

                string[] words = block.words;

                drawSetColor(block.color);
                ImGui.PushStyleColor(ImGuiCol.Text, block.color);

                for(int i = 0; i < words.Length; i++) {
                    string word  = words[i] + " ";
                    var textSize = ImGui.CalcTextSize(word.Replace("%%", "%"));
                    
                    if(currLine + textSize.X > wrapWidth) {
                        currLine = 0;
                        _x =  x;
                        _y += lineHeight;
                        height += lineHeight;
                    }

                    ImGui.SetCursorPos(new(_x, _y));
                    ImGui.TextUnformatted(word.Replace("%%", "%"));

                    currLine += textSize.X;
                    _x += textSize.X;
                }

                ImGui.PopStyleColor();
            }

            if(currLine > 0) height += lineHeight;
            drawSetColor(Vector4.One);
            return height;
        }
        #endregion

        #region image
        public static void drawImage(LoadedTexture texture, float x, float y, float width, float height) {
            if (texture == null) return;
            ImGui.SetCursorPos(new (x, y));
            ImGui.Image(texture.TextureId, new (width, height), new(0,0), new(1,1), drawCurrentColor);
        }

        public static void drawImageUV(LoadedTexture texture, float x, float y, float width, float height, float u0, float v0, float u1, float v1) {
            if (texture == null) return;
            ImGui.SetCursorPos(new (x, y));
            ImGui.Image(texture.TextureId, new (width, height), new(u0,v0), new(u1,v1), drawCurrentColor);
        }

        public static void drawImage9patch(LoadedTexture texture, float x, float y, float width, float height, float padd) {
            if (texture == null) return;
            float padw = padd / texture.Width;
            float padh = padd / texture.Height;

            float sp0_x = x;
            float sp1_x = x + padd;
            float sp2_x = x + width - padd;
            float sp3_x = x + width;

            float sp0_y = y;
            float sp1_y = y + padd;
            float sp2_y = y + height - padd;
            float sp3_y = y + height;

            float sp0_u = 0;
            float sp1_u = padw;
            float sp2_u = 1 - padw;
            float sp3_u = 1;

            float sp0_v = 0;
            float sp1_v = padh;
            float sp2_v = 1 - padh;
            float sp3_v = 1;

            ImGui.SetCursorPos(new (sp0_x, sp0_y));
            ImGui.Image(texture.TextureId, new(padd, padd), new(sp0_u, sp0_v), new(sp1_u, sp1_v), drawCurrentColor);

            ImGui.SetCursorPos(new (sp1_x, sp0_y));
            ImGui.Image(texture.TextureId, new(width - padd * 2, padd), new(sp1_u, sp0_v), new(sp2_u, sp1_v), drawCurrentColor);

            ImGui.SetCursorPos(new (sp2_x, sp0_y));
            ImGui.Image(texture.TextureId, new(padd, padd), new(sp2_u, sp0_v), new(sp3_u, sp1_v), drawCurrentColor);

            
            ImGui.SetCursorPos(new (sp0_x, sp1_y));
            ImGui.Image(texture.TextureId, new(padd, height - padd * 2), new(sp0_u, sp1_v), new(sp1_u, sp2_v), drawCurrentColor);

            ImGui.SetCursorPos(new (sp1_x, sp1_y));
            ImGui.Image(texture.TextureId, new(width - padd * 2, height - padd * 2), new(sp1_u, sp1_v), new(sp2_u, sp2_v), drawCurrentColor);

            ImGui.SetCursorPos(new (sp2_x, sp1_y));
            ImGui.Image(texture.TextureId, new(padd, height - padd * 2), new(sp2_u, sp1_v), new(sp3_u, sp2_v), drawCurrentColor);

            
            ImGui.SetCursorPos(new (sp0_x, sp2_y));
            ImGui.Image(texture.TextureId, new(padd, padd), new(sp0_u, sp2_v), new(sp1_u, sp3_v), drawCurrentColor);

            ImGui.SetCursorPos(new (sp1_x, sp2_y));
            ImGui.Image(texture.TextureId, new(width - padd * 2, padd), new(sp1_u, sp2_v), new(sp2_u, sp3_v), drawCurrentColor);

            ImGui.SetCursorPos(new (sp2_x, sp2_y));
            ImGui.Image(texture.TextureId, new(padd, padd), new(sp2_u, sp2_v), new(sp3_u, sp3_v), drawCurrentColor);

        }

        public static void drawImageFitOverflow(LoadedTexture texture, float x, float y, float width, float height, float bias = 0.5f) {
            if (texture == null) return;
            float tw = texture.Width;
            float th = texture.Height;
            
            Vector2 uv0 = new Vector2(0, 0);
            Vector2 uv1 = new Vector2(1, 1);

            if(width > height) {
                float vv = height / width;
                uv0.Y = (1f - vv) * bias;
                uv1.Y = uv0.Y + vv;

            } else if(height > width) {
                float uu = height / width;
                uv0.X = (1f - uu) * bias;
                uv1.X = uv0.X + uu;

            }

            ImGui.SetCursorPos(new (x, y));
            ImGui.Image(texture.TextureId, new (width, height), uv0, uv1, drawCurrentColor);
        }

        public static bool drawButton(string text, float x, float y, float width = 0, float height = 0) {
            ImGui.SetCursorPos(new (x, y));
            return ImGui.Button(text, new (width, height));
        }
        
        public static void drawProgressBar(float progress, float x, float y, float width, float height, Vector4 colorBG, Vector4 colorFG) {
            // ImGui.SetCursorPos(new (x, y));
            // ImGui.ProgressBar(progress, new (width, height), label);

            drawSetColor(colorBG);
            drawImage(resourceLoader.Sprite("xSkillGilded:textures/gui/skilltree/elements/pixel.png"), x, y, width, height);
            drawSetColor(colorFG);
            drawImage(resourceLoader.Sprite("xSkillGilded:textures/gui/skilltree/elements/pixel.png"), x, y, width * progress, height);
            drawSetColor(Vector4.One);
        }
        #endregion

        #region IO
        public static bool mouseHover(float x0, float y0, float x1, float y1) {
            return ImGui.IsMouseHoveringRect(new (windowPosX + x0, windowPosY + y0), new (windowPosX + x1, windowPosY + y1));
        }
        #endregion

        #region Utils
        public static LoadedTexture Sprite(string cat, string name) {
            return resourceLoader.Sprite($"xskillgilded:textures/gui/skilltree/{cat}/{name}.png"); 
        }

        public static float _ui(float v)
        {
            return v * uiScale;
        }

        public static float lerpLinearTo(float a, float b, float t, float dt) {
            if(Math.Abs(a - b) <= t * dt) return b;

            return a + Math.Sign(b - a) * t * dt;
        }

        public static float lerpTo(float a, float b, float t, float dt) {
            if(Math.Abs(a - b) < 0.01f) return b;

            var _rat = 1 - Math.Pow(1 - t, dt * 10f);
            return a + (b - a) * (float)_rat;
        }
        
        public static Vector4 hexToVec4(string _hex) {
            var hex = _hex.Replace("#", "");

            if (hex.Length == 6) {
                var r = Convert.ToInt32(hex.Substring(0,2), 16) / 255f;
                var g = Convert.ToInt32(hex.Substring(2,2), 16) / 255f;
                var b = Convert.ToInt32(hex.Substring(4,2), 16) / 255f;
                return new Vector4(r, g, b, 1);
            }

            if (hex.Length == 8) {
                var r = Convert.ToInt32(hex.Substring(0,2), 16) / 255f;
                var g = Convert.ToInt32(hex.Substring(2,2), 16) / 255f;
                var b = Convert.ToInt32(hex.Substring(4,2), 16) / 255f;
                var a = Convert.ToInt32(hex.Substring(6,2), 16) / 255f;
                return new Vector4(r, g, b, a);
            }

            return new Vector4(1,1,1,1);
        }

        public static string FormatTime(float sec) {
            int h = (int)Math.Floor(sec / 3600);
            sec -= h * 3600;
            int m = (int)Math.Floor(sec / 60);
            sec -= m * 60;
            int s = (int)Math.Floor(sec);

            if(h > 0) return $"{h:D2}:{m:D2}:{s:D2}";
            return $"{m:D2}:{s:D2}";
        }
        
        public static bool pointInRectangle(float px, float py, float x0, float y0, float x1, float y1) {
            return px >= x0 && py >= y0 && px <= x1 && py <= y1;
        }
        #endregion
    }
}
