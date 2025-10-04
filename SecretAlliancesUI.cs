using System;
using System.Linq;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace SecretAlliances.UI
{
    public static class SecretAlliancesUI
    {
        private static GauntletLayer _layer;
        private static ViewModel _vm;

        public static bool IsOpen { get { return _layer != null; } }

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Open()
        {
            if (_layer != null) return;

            _vm = CreateAllianceManagerVM();

            _layer = new GauntletLayer(200);
            _layer.IsFocusLayer = true;
            _layer.LoadMovie("AllianceManager", _vm);

            ScreenManager.TopScreen.AddLayer(_layer);
            ScreenManager.TrySetFocus(_layer);
        }

        public static void Close()
        {
            if (_layer == null) return;
            ScreenManager.TopScreen.RemoveLayer(_layer);
            _layer = null;
            _vm = null;
        }

        private static ViewModel CreateAllianceManagerVM()
        {
            var asm = typeof(SecretAlliances.SubModule).Assembly;
            var type = asm.GetTypes().FirstOrDefault(t => t.FullName == "SecretAlliances.ViewModels.AllianceManagerVM");
            if (type == null)
                throw new InvalidOperationException("AllianceManagerVM type not found in SecretAlliances.ViewModels.");

            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor != null) return (ViewModel)ctor.Invoke(null);

            var actionCtor = type.GetConstructor(new[] { typeof(Action) });
            if (actionCtor != null) return (ViewModel)actionCtor.Invoke(new object[] { new Action(Close) });

            var anyCtor = type.GetConstructors().OrderBy(c => c.GetParameters().Length).First();
            var parms = anyCtor.GetParameters();
            var args = new object[parms.Length];
            for (int i = 0; i < parms.Length; i++)
                args[i] = parms[i].HasDefaultValue ? parms[i].DefaultValue : (parms[i].ParameterType.IsValueType ? Activator.CreateInstance(parms[i].ParameterType) : null);

            return (ViewModel)anyCtor.Invoke(args);
        }
    }
}