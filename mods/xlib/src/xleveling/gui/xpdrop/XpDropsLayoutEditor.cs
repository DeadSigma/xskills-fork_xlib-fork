using System;
using System.Collections.Generic;

namespace PandaXPDrops
{
    /// <summary>
    /// Общий реестр перемещаемых HUD-элементов для F6-редактора макета.
    /// Позволяет сторонним модам (например, XSkills) добавлять свои элементы в тот же
    /// редактор, не создавая жёсткой зависимости: все сигнатуры используют только типы BCL,
    /// поэтому регистрироваться можно и напрямую, и через reflection.
    /// </summary>
    public static class XpDropsLayoutEditor
    {
        /// <summary>Один внешний перемещаемый элемент.</summary>
        public sealed class Editable
        {
            /// <summary>Отображаемое имя (для отладки/подсказок).</summary>
            public string Name;

            /// <summary>Текущий экранный прямоугольник элемента в РЕАЛЬНЫХ пикселях: [x, y, w, h]. Может вернуть null.</summary>
            public Func<double[]> GetRect;

            /// <summary>Ставит левый-верхний угол элемента в заданную экранную точку (реальные пиксели).</summary>
            public Action<double, double> SetTopLeft;

            /// <summary>Необязательно: масштабирование колесиком (шаг +/-). Может быть null.</summary>
            public Action<float> OnScale;

            /// <summary>Необязательно: вызывается при закрытии редактора - момент сохранить позицию на диск. Может быть null.</summary>
            public Action OnCommit;

            internal Editable() { }
        }

        private static readonly List<Editable> items = new List<Editable>();

        /// <summary>Все зарегистрированные внешние элементы.</summary>
        public static IReadOnlyList<Editable> Items => items;

        /// <summary>
        /// Открыт ли сейчас F6-редактор. Внешний HUD может это читать, чтобы на время
        /// редактирования отключить собственный перехват мыши (иначе клик по нему не дойдёт
        /// до редактора).
        /// </summary>
        public static bool IsEditing { get; internal set; }

        /// <summary>Регистрирует элемент. Возвращает токен (тип object, чтобы reflection-вызов не знал внутренний тип).</summary>
        public static object Register(string name, Func<double[]> getRect, Action<double, double> setTopLeft, Action<float> onScale, Action onCommit)
        {
            if (getRect == null || setTopLeft == null) return null;

            Editable e = new Editable
            {
                Name = name,
                GetRect = getRect,
                SetTopLeft = setTopLeft,
                OnScale = onScale,
                OnCommit = onCommit
            };
            items.Add(e);
            return e;
        }

        /// <summary>Снимает регистрацию по токену, полученному из <see cref="Register"/>.</summary>
        public static void Unregister(object token)
        {
            if (token is Editable e) items.Remove(e);
        }
    }
}
