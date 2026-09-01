using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vintagestory.API.Client;
using System.Text.Json;
using System.Net.Http.Headers;

namespace xSkillGilded {
    
    public class atlasBBOX {
        public Vector2 uv0, uv1; 
        public Vector2 size;

        public atlasBBOX(float u0, float v0, float u1, float v1, float width, float height) {
            uv0  = new(u0, v0);
            uv1  = new(u1, v1);
            size = new(width, height);
        }
    }

    public class atlasJson {
        public float  x      {get;set;}
        public float  y      {get;set;}
        public float  width  {get;set;}
        public float  height {get;set;}

        public int value     {get;set;}
        public int advanceX  {get;set;}
    }

    public class Font {
        private LoadedTexture texture;
        protected Dictionary<char, atlasBBOX> atlas;

        public Font() {}
        public float letterSpacing = 0;
        public float spaceWidth    = 0;
        public float baseScale     = 1;
        public float baseLineHeight;

        public float getLineHeight() { return baseLineHeight * baseScale; } 

        public bool hasFallbackColor = false;
        public Vector4 fallbackColor;

        public Font LoadedTexture(ICoreClientAPI api, LoadedTexture texture, string jsonMap) {
            this.texture = texture;
            baseLineHeight = 0;

            atlas = new Dictionary<char, atlasBBOX>();
            atlasJson[] data = JsonSerializer.Deserialize<atlasJson[]>(jsonMap);

            for(int i = 0; i < data.Length; i++) {
                atlasJson d = data[i];

                char  ch = (char)d.value;
                float u0 = d.x / texture.Width;
                float v0 = d.y / texture.Height;
                float u1 = (d.x + d.width) / texture.Width;
                float v1 = (d.y + d.height) / texture.Height;

                float w = d.width;
                float h = d.height;
                baseLineHeight = Math.Max(baseLineHeight, h);

                if(ch == 'l') spaceWidth = w;

                atlas[ch] = new atlasBBOX(u0, v0, u1, v1, w, h);
            }

            return this;
        }

        public Font setLetterSpacing(float space) {
            letterSpacing = space;
            return this;
        }

        public Font setFallbackColor(Vector4 color) {
            fallbackColor = color;
            hasFallbackColor = true;
            return this;
        }

        public Vector2 CalcTextSize(string text) {
            Vector2 size = new();

            foreach (char c in text) {
                switch(c) {
                    case ' '  : size.X += spaceWidth;     continue;
                    case '\t' : size.X += spaceWidth * 2; continue;
                }

                if(!atlas.ContainsKey(c)) continue;

                atlasBBOX bbox = atlas[c];
                size.X += bbox.size.X + letterSpacing;
                size.Y  = Math.Max(size.Y, bbox.size.Y);
            }

            size.X *= baseScale;
            size.Y *= baseScale;

            return size;
        }

        public float drawChar(char c) {
            switch(c) {
                case ' '  : return spaceWidth;
                case '\t' : return spaceWidth * 2;
            }

            if(!atlas.ContainsKey(c)) return 0;

            atlasBBOX box = atlas[c];
            ImGui.Image(texture.TextureId, new(box.size.X * baseScale, box.size.Y * baseScale), box.uv0, box.uv1);
            return (box.size.X + letterSpacing) * baseScale;
        }
        
        public float drawCharColor(char c, Vector4 color) {
            switch(c) {
                case ' '  : return spaceWidth;
                case '\t' : return spaceWidth * 2;
            }

            if(!atlas.ContainsKey(c)) return 0;

            atlasBBOX box = atlas[c];
            ImGui.Image(texture.TextureId, new(box.size.X * baseScale, box.size.Y * baseScale), box.uv0, box.uv1, color);
            return (box.size.X + letterSpacing) * baseScale;
        }
    }
}
