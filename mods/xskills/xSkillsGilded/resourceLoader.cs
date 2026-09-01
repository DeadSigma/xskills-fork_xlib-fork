using ImGuiController_OpenTK;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.Common;
using xSkillGilded;
using static System.Net.Mime.MediaTypeNames;

namespace xSkillGilded {
    static class resourceLoader {
        public static ICoreClientAPI api = null;
        public static void setApi(ICoreClientAPI _api) { api = _api; }

        public static LoadedTexture Sprite(string name) {
            LoadedTexture tex = new(api);
            api.Render.GetOrLoadTexture(name, ref tex);
            return tex;
        }

        public static ImFontPtr loadFont(string path) {
            string _fpath = Path.Combine(GamePaths.AssetsPath, "xskillgilded", "fonts", "scarab.ttf");
            ImGuiIOPtr io = ImGui.GetIO();
            ImFontPtr f = io.Fonts.AddFontFromFileTTF(_fpath, 24);
            return f;
        }
        
    }

}