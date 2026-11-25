using System;
using System.Linq;
using PocketPutt.Game;
using PocketPutt.Levels;
using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>Bottom sheet for the selected map node: number/title, stars or the reason it is locked, and the single contextual action.</summary>
    public sealed class SelectedLevelPanel : VisualElement
    {
        public const float Height = 340f;
        public event Action<string> PlayRequested;
        public string LevelId { get; private set; }
        public VisualElement PlayButton { get; private set; }

        float safeBottom;

        public SelectedLevelPanel()
        {
            name = "hub-panel";
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.bottom = 0;
            style.backgroundColor = Pal.Cream;
            style.borderTopLeftRadius = 60; style.borderTopRightRadius = 60;
            style.borderTopWidth = 5;
            style.borderTopColor = Pal.A(Pal.CreamDark, 1f);
            style.display = DisplayStyle.None;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            pickingMode = PickingMode.Position;   // swallow taps so they never reach the scroller behind the sheet
        }

        public void SetSafeBottom(float px)
        {
            safeBottom = px;
            style.height = Height + px;
            Ui.Pad(this, 36, 24, 36, 24 + px);
        }

        public void Hide()
        {
            LevelId = null;
            style.display = DisplayStyle.None;
        }

        public void Show(Progress p, CupDef cup, int cupIndex, string levelId, ThemeColors tc)
        {
            LevelId = levelId;
            Clear();
            LevelData lv = LevelLibrary.Get(levelId);
            int idx = Array.IndexOf(cup.levelIds, levelId);
            bool isFinal = levelId == cup.FinalId;
            LevelState st = p.StateOf(levelId);
            bool locked = st == LevelState.Locked;
            bool won = p.HasTrophy(cup.id);

            // badge: level number (or the cup trophy for the final)
            var badge = new VisualElement();
            badge.style.width = 132; badge.style.height = 132;
            Ui.Radius(badge, 66);
            badge.style.backgroundColor = locked ? Pal.H("8D897E") : st == LevelState.Completed ? Pal.Green : Pal.A(tc.accent, 1f);
            badge.style.borderBottomWidth = 10;
            badge.style.borderBottomColor = locked ? Pal.H("55524B") : st == LevelState.Completed ? Pal.GreenDark : tc.accentDark;
            Ui.Center(badge);
            badge.pickingMode = PickingMode.Ignore;
            if (isFinal)
            {
                var tr = new TrophyElement(cupIndex, 104);
                tr.SetSilhouette(!won);
                badge.Add(tr);
            }
            else badge.Add(Ui.Text((idx + 1).ToString(), 76, Color.white));
            Add(badge);

            var col = new VisualElement();
            col.style.flexGrow = 1; col.style.flexShrink = 1;
            col.style.marginLeft = 28; col.style.marginRight = 18;
            col.pickingMode = PickingMode.Ignore;
            Add(col);

            string title = isFinal ? Loc.T("cup.final") : locked ? Loc.T("hub.locked") : Loc.Pick(lv?.title ?? levelId);
            col.Add(Ui.Text(title, isFinal ? 52 : 46, locked ? Pal.Slate : Pal.Ink, true, TextAnchor.MiddleLeft));

            if (locked)
            {
                if (isFinal && p.CupUnlocked(cupIndex)) AddGateRows(col, p, cup);
                else
                {
                    var why = Ui.Text(HubLogic.LockReason(p, levelId) ?? "", 32, Pal.Slate, false, TextAnchor.MiddleLeft);
                    why.style.marginTop = 6;
                    col.Add(why);
                }
            }
            else
            {
                if (isFinal)
                {
                    var sub = Ui.Text(Loc.Pick(lv?.title ?? levelId), 34, Pal.Slate, false, TextAnchor.MiddleLeft);
                    sub.style.marginTop = 2;
                    col.Add(sub);
                }
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginTop = 10;
                row.pickingMode = PickingMode.Ignore;
                var sr = new StarRow(50, 6);
                sr.Set(p.Stars(levelId));
                sr.style.justifyContent = Justify.FlexStart;
                row.Add(sr);
                col.Add(row);
                string info = st == LevelState.Completed
                    ? Loc.T("res.best", p.BestShots(levelId))
                    : (lv != null ? Loc.T("hud.par", lv.threeStar) : "");
                var l = Ui.Text(info, 32, Pal.Slate, false, TextAnchor.MiddleLeft);
                l.style.marginTop = 6;
                col.Add(l);
            }

            if (!locked)
            {
                string label = Loc.T(st == LevelState.Completed ? "hub.replay" : "menu.play");
                PlayButton = Ui.Button(label, Pal.Green, Pal.GreenDark, Color.white, 350, 128, 46, () => PlayRequested?.Invoke(levelId));
                foreach (Label bl in PlayButton.Children().OfType<Label>()) bl.style.whiteSpace = WhiteSpace.NoWrap;
                PlayButton.name = "hub-play";
                PlayButton.Insert(0, new IconElement(Icon.Play, 60, Color.white));
                Add(PlayButton);
            }
            else PlayButton = null;

            style.display = DisplayStyle.Flex;
            style.translate = new Translate(0, 40);
            style.opacity = 0f;
            experimental.animation.Start(0f, 1f, 200, (e, t) =>
            {
                e.style.opacity = t;
                e.style.translate = new Translate(0, (1f - t) * 40f);
            });
        }

        static void AddGateRows(VisualElement col, Progress p, CupDef cup)
        {
            FinalGate g = HubLogic.GateOf(p, cup);
            col.Add(GateRow(Loc.T("hub.qual", g.qualifiersDone, g.qualifiersTotal), !g.needQualifiers));
            col.Add(GateRow(Loc.T("hub.qstars", g.stars, g.threshold), !g.needStars));
        }

        static VisualElement GateRow(string text, bool met)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6;
            row.pickingMode = PickingMode.Ignore;
            row.Add(new IconElement(met ? Icon.Check : Icon.Lock, 40, met ? Pal.GreenDark : Pal.Slate));
            var l = Ui.Text(text, 34, met ? Pal.GreenDark : Pal.Ink, true, TextAnchor.MiddleLeft);
            l.style.marginLeft = 10;
            row.Add(l);
            return row;
        }
    }
}
