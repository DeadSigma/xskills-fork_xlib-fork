
namespace XLib.XEffects
{
    /// <summary>
    /// Configuration class for XEffects.
    /// </summary>
    public class XEffectsConfig
    {
        /// <summary>
        /// the interval in which effects are checked
        /// </summary>
        public float effectInterval = 0.25f;

        /// <summary>
        /// the interval in which effect triggers are checked
        /// </summary>
        public float tiggerInterval = 10.0f;

        /// <summary>
        /// The persisted Effects HUD mode.
        /// -1 means never shown, 0 means dynamic, 1 means always shown.
        /// </summary>
        public int effectFrameState = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="XEffectsConfig"/> class.
        /// </summary>
        public XEffectsConfig()
        { }
    }//!XEffectsConfig
}//!XLib.XEffects
