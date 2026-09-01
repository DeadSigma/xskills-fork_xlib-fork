using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xSkillGilded
{
    public class ModConfig
    {
        public bool lvPopupEnabled { get; set; } = true;

        public bool effectBoxEnabled { get; set; } = true;
        public float effectBoxOriginX { get; set; } = 8f;
        public float effectBoxOriginY { get; set; } = 8f;
        public float effectBoxSize { get; set; } = 40f;
        public int effectBoxOrientation { get; set; } = 0;

        public bool EnableCustomFont { get; set; } = true;
        public string _comment_CustomFontPath { get; set; } = "Optional: absolute path to a .ttf font. Windows: C:\\Windows\\Fonts\\simhei.ttf | Linux: /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf (single forward slashes). Flatpak: path is resolved inside the sandbox - use fonts under /app/extra/vintagestory/assets or run 'flatpak run --command=fc-list <appid>' to list them";
        public string CustomFontPath { get; set; } = "";

        // scarab есть только для английского - этот флаг выключает его и там, рисуя всё системным ImGui-шрифтом
        public string _comment_ForceDisableScarabFont { get; set; } = "If true - disables the baked scarab font even in English and uses the ImGui system font everywhere. This is useful if you have a 4K monitor and want to scale the text.";
        public bool ForceDisableScarabFont { get; set; } = false;

        /// <summary>Игрок хоть раз двигал окно. Пока false - окно центрируется, как раньше</summary>
        public bool windowPosSet = false;

        /// <summary>Позиция окна относительно viewport.Pos, а не абсолютная</summary>
        public int windowX = 0;
        public int windowY = 0;

        /// <summary>Множитель размера поверх ClientSettings.GUIScale</summary>
        public float windowScale = 1f;

        /// <summary>Игрок хоть раз двигал попап. Пока false - центр сверху, как раньше</summary>
        public bool levelPopupPosSet = false;

        /// <summary>Позиция попапа относительно viewport.Pos</summary>
        public int levelPopupX = 0;
        public int levelPopupY = 0;

        /// <summary>Множитель размера попапа поверх ClientSettings.GUIScale</summary>
        public float levelPopupScale = 1f;
    }
}