using UnityEngine;

namespace ANL.UI.Screens
{
    public abstract class AnlScreen
    {
        public GameObject Root { get; protected set; }
        public UiKit Kit { get; protected set; }
        public AppRoot App { get; protected set; }
        public bool IsModal { get; protected set; }

        protected AnlScreen(AppRoot app)
        {
            App = app;
            Kit = app.Kit;
        }

        public abstract void Build();

        public virtual void Refresh()
        {
        }

        public void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.SetActive(visible);
                if (visible) Refresh();
            }
        }

        public bool Visible
        {
            get { return Root != null && Root.activeSelf; }
        }

        protected void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
        }
    }
}
