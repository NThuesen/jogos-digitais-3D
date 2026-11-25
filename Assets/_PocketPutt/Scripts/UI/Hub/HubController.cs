using System;
using PocketPutt.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>Owns the two hub screens (destinations overview, cup map). Presentation only: navigation intent comes from GameApp.</summary>
    public sealed class HubController
    {
        public enum View { None, Overview, Map }

        readonly GameApp app;
        readonly CupOverviewView overview;
        readonly CupMapView map;
        readonly Action onChanged;

        public View Current { get; private set; }
        public CupMapView Map => map;
        public CupOverviewView Overview => overview;

        public HubController(GameApp app, VisualElement parent, Func<Vector4> safeInsets)
        {
            this.app = app;
            overview = new CupOverviewView(app, safeInsets);
            map = new CupMapView(app, safeInsets);
            parent.Add(overview);
            parent.Add(map);
            onChanged = Refresh;
            app.Progress.Changed += onChanged;
        }

        /// <summary>Detaches from Progress (the UI is rebuilt after a language change; no callbacks may leak).</summary>
        public void Dispose()
        {
            if (app != null && app.Progress != null) app.Progress.Changed -= onChanged;
        }

        public void ShowOverview(int focusCup)
        {
            map.Close();
            overview.Open(focusCup);
            Ui.Fade(overview, 0f, 1f, 200);
            Current = View.Overview;
        }

        public void ShowMap(int cup, string selectLevelId)
        {
            overview.Close();
            map.Open(cup, selectLevelId);
            Ui.Fade(map, 0f, 1f, 200);
            Current = View.Map;
        }

        public void Hide()
        {
            overview.Close();
            map.Close();
            Current = View.None;
        }

        /// <summary>Back inside the hub: map -> destinations. Returns false when the hub has nowhere to go back to (overview -> menu).</summary>
        public bool HandleBack()
        {
            if (Current != View.Map) return false;
            app.ShowCupOverview(map.CupIndex);
            return true;
        }

        void Refresh()
        {
            if (Current == View.Map) map.Refresh();
            else if (Current == View.Overview) overview.Refresh();
        }

        public void Tick(float dt)
        {
            if (Current == View.Map) map.Tick(dt);
            else if (Current == View.Overview) overview.Tick(dt);
        }
    }
}
