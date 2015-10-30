using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;
using System.Reflection;
namespace LiveSplit.KTANE {
    public class KTANEFactory : IComponentFactory {
        public string ComponentName { get { return "Keep Talking and Nobody Explodes Autosplitter v" + this.Version.ToString(); } }
        public string Description { get { return "Autosplitter for Keep Talking and Nobody Explodes"; } }
        public ComponentCategory Category { get { return ComponentCategory.Control; } }
        public IComponent Create(LiveSplitState state) { return new KTANEComponent(); }
        public string UpdateName { get { return this.ComponentName; } }
        public string UpdateURL { get { return ""; } }
        public string XMLURL { get { return ""; } }
        public Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
    }
}