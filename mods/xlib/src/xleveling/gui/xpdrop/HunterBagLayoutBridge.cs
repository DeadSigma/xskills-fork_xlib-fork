using System;
using System.Reflection;

namespace XSkills
{
    /// <summary>
    /// Всё через reflection, чтобы XSkills не получил жёсткую зависимость от него: если редактора нет - слот просто не будет перемещаемым, без ошибок
    /// Все сигнатуры реестра используют только типы BCL, поэтому reflection тривиален
    /// </summary>
    public static class HunterBagLayoutBridge
    {
        private const string EditorTypeName = "PandaXPDrops.XpDropsLayoutEditor";

        private static bool resolved;
        private static MethodInfo registerM;
        private static MethodInfo unregisterM;
        private static PropertyInfo isEditingP;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            Type t = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(EditorTypeName); }
                catch { t = null; }
                if (t != null) break;
            }
            if (t == null) return;

            registerM = t.GetMethod("Register", new[]
            {
                typeof(string), typeof(Func<double[]>), typeof(Action<double, double>), typeof(Action<float>), typeof(Action)
            });
            unregisterM = t.GetMethod("Unregister", new[] { typeof(object) });
            isEditingP = t.GetProperty("IsEditing", BindingFlags.Public | BindingFlags.Static);
        }

        /// <summary>Открыт ли сейчас редактор макета. false, если PandaXPDrops отсутствует</summary>
        public static bool IsEditing
        {
            get
            {
                try
                {
                    Resolve();
                    return isEditingP?.GetValue(null) is bool b && b;
                }
                catch { return false; }
            }
        }

        /// <summary>Регистрирует перемещаемый элемент. Возвращает токен или null, если редактора нет</summary>
        public static object Register(string name, Func<double[]> getRect, Action<double, double> setTopLeft, Action<float> onScale, Action onCommit)
        {
            try
            {
                Resolve();
                return registerM?.Invoke(null, new object[] { name, getRect, setTopLeft, onScale, onCommit });
            }
            catch { return null; }
        }

        /// <summary>Снимает регистрацию по токену из <see cref="Register"/></summary>
        public static void Unregister(object token)
        {
            if (token == null) return;
            try
            {
                Resolve();
                unregisterM?.Invoke(null, new object[] { token });
            }
            catch { }
        }
    }
}