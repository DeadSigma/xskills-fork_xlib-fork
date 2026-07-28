using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace XLib.XEffects
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="HudElement" />
    public class EffectFrame : HudElement
    {
        /// <summary>
        /// Gets the toggle key combination code.
        /// </summary>
        /// <value>
        /// The toggle key combination code.
        /// </value>
        public override string ToggleKeyCombinationCode { get => "effectframehotkey"; }

        /// <summary>
        /// Gets or sets the state of the element.
        /// 0 means dynamic open/close
        /// -1 means never shown
        /// 1 means always shown
        /// </summary>
        /// <value>
        /// The state of the forced.
        /// </value>
        public int ForcedState { get; set; }

        /// <summary>
        /// Gets or sets the width of the text.
        /// </summary>
        /// <value>
        /// The width of the text.
        /// </value>
        public int TextWidth { get; set; }

        /// <summary>
        /// The text list
        /// </summary>
        List<EffectBox> effectBoxes;

        /// <summary>
        /// A hud element that shows the effect tooltip.
        /// </summary>
        private EffectTooltip effectTooltip;

        /// <summary>
        /// Called whenever the Effects HUD mode changes.
        /// </summary>
        private readonly Action<int> forcedStateChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="EffectFrame"/> class.
        /// </summary>
        /// <param name="capi">The Client API</param>
        public EffectFrame(ICoreClientAPI capi)
            : this(capi, 0, null)
        {
        }

        /// <summary>
        /// Initializes a new Effects HUD with a persisted mode and change callback.
        /// </summary>
        /// <param name="capi">The Client API.</param>
        /// <param name="forcedState">-1 = never, 0 = dynamic, 1 = always.</param>
        /// <param name="forcedStateChanged">Called after the mode changes.</param>
        public EffectFrame(
            ICoreClientAPI capi,
            int forcedState,
            Action<int> forcedStateChanged
        ) : base(capi)
        {
            ForcedState = Math.Clamp(forcedState, -1, 1);
            TextWidth = 200;
            effectBoxes = new List<EffectBox>();
            effectTooltip = null;
            this.forcedStateChanged = forcedStateChanged;
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public void Update()
        {
            AffectedEntityBehavior affected = this.capi.World.Player.Entity.GetBehavior("Affected") as AffectedEntityBehavior;
            if (affected == null) return;
            if (affected.Effects.Count == 0 && ForcedState != 1 || ForcedState == -1)
            {
                TryClose();
                return;
            }

            if(affected.Effects.Count == this.effectBoxes.Count)
            {
                Dictionary<string, Effect>.Enumerator effect = affected.Effects.GetEnumerator();
                foreach (EffectBox box in effectBoxes)
                {
                    effect.MoveNext();
                    box.Effect = effect.Current.Value;
                    box.Update();
                }
                if (!this.IsOpened()) this.TryOpen();
                return;
            }

            effectBoxes = new List<EffectBox>();

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.LeftMiddle);
            ElementBounds bgBounds = ElementBounds.Fill;
            ElementBounds boxBounds;

            if (affected.Effects.Count != 0)
            {
                bgBounds.BothSizing = ElementSizing.FitToChildren;
            }
            else
            {
                if (effectBoxes.Count == 0 && this.IsOpened()) return;
                bgBounds.BothSizing = ElementSizing.Fixed;
                bgBounds.WithFixedSize(32 + TextWidth, 32);
            }

            dialogBounds.WithChild(bgBounds);

            this.effectTooltip?.TryClose();
            this.effectTooltip = this.effectTooltip ?? new EffectTooltip(this.capi, dialogBounds);

            SingleComposer = capi.Gui.CreateCompo("EffectFrame", dialogBounds)
                .AddGrayBG(bgBounds)
                .AddDialogTitleBar("Effects", OnTitleBarClose);

            int yy = 32;
            foreach (Effect effect in affected.Effects.Values)
            {
                boxBounds = ElementBounds.FixedPos(EnumDialogArea.LeftTop, 32, yy).WithFixedSize(32 + TextWidth, 32);

                bgBounds.WithChildren(boxBounds);
                EffectBox box = new EffectBox(capi, effect, boxBounds, effectTooltip);
                effectBoxes.Add(box);
                SingleComposer.AddInteractiveElement(box);
                box.AddToComposer(SingleComposer);
                yy += 32;
            }
            SingleComposer.Compose();
            this.TryOpen();

            foreach(EffectBox box in effectBoxes)
            {
                box.OnMouseMove(this.capi.Input.MouseX, this.capi.Input.MouseY);
            }
        }

        /// <summary>
        /// Called when the close button on the title bar was clicked.
        /// </summary>
        public void OnTitleBarClose()
        {
            SetForcedState(this.ForcedState - 1);
            TryClose();
        }

        /// <summary>
        /// Changes the Effects HUD mode and notifies the owning system so the
        /// selection can be persisted.
        /// </summary>
        /// <param name="state">-1 = never, 0 = dynamic, 1 = always.</param>
        public void SetForcedState(int state)
        {
            int normalizedState = Math.Clamp(state, -1, 1);

            if (this.ForcedState == normalizedState)
            {
                return;
            }

            this.ForcedState = normalizedState;
            this.forcedStateChanged?.Invoke(normalizedState);

            if (normalizedState == -1 && this.IsOpened())
            {
                TryClose();
            }
        }

        /// <summary>
        /// Called every time this dialog closes.
        /// </summary>
        /// <returns></returns>
        public override bool TryClose()
        {
            if (this.effectTooltip != null)
            {
                this.effectTooltip.TryClose();
                //this.effectTooltip = null;
            }
            return base.TryClose();
        }
    }//!class EffectFrame
}//!namespace XLib.XEffects
