#nullable enable

using CityBuilder.Client.Presentation.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityBuilder.Client.Presentation.Feedback
{
    /// <summary>
    /// The toasts on the screen (M0-47): a thin component over a <see cref="ToastQueue"/>. It loads <c>UI/Toasts</c> from <c>Resources</c>,
    /// writes one label per visible toast into the list at the top of the screen when the queue changes, and asks the queue every frame to
    /// drop the toasts whose time is up. It takes no pointer (nothing in the layout picks), so the ground below still gets every press, and
    /// it decides nothing: the sentences come from <see cref="FeedbackMessages"/> and the times from the queue. <c>GameRoot</c> creates it
    /// at the start, with <see cref="Create"/>, so a result of the connection or a command always has a place to show.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToastHost : MonoBehaviour
    {
        /// <summary>The toasts layout (UXML), loaded by name from <c>Assets/_Project/Resources</c>.</summary>
        private const string LayoutPath = "UI/Toasts";

        /// <summary>The panel settings the UI shares with the other screens.</summary>
        private const string PanelSettingsPath = "UI/PanelSettings";

        /// <summary>The root's padding in panel units before the safe area is added.</summary>
        private const float BasePadding = 16f;

        /// <summary>Drawn above the build menu (1) and the placement bar (2): a result must not be hidden by the screen that caused it.</summary>
        private const float SortingOrder = 3f;

        /// <summary>The name of the root element in <c>Toasts.uxml</c>.</summary>
        public const string RootName = "toast-root";

        /// <summary>The name of the element the toasts are added to.</summary>
        public const string ListName = "toast-list";

        /// <summary>The USS class of every toast label.</summary>
        public const string ToastClass = "toast";

        /// <summary>The USS class of a success toast.</summary>
        public const string SuccessClass = "toast--success";

        /// <summary>The USS class of an error toast.</summary>
        public const string ErrorClass = "toast--error";

        /// <summary>The queue whose toasts are shown; null before <see cref="Create"/>.</summary>
        private ToastQueue? _queue;

        /// <summary>The UI document of the toasts; null before <see cref="Create"/> made it.</summary>
        private UIDocument? _document;

        /// <summary>The element the toast labels are added to; null until <c>Start</c> found it.</summary>
        private VisualElement? _list;

        /// <summary>The queue the host shows.</summary>
        public ToastQueue? Queue => _queue;

        /// <summary>The root of the toasts' element tree; null when the layout was not loaded.</summary>
        public VisualElement? Root => _document == null ? null : _document.rootVisualElement;

        /// <summary>How many toast labels the list holds now.</summary>
        public int ShownCount => _list == null ? 0 : _list.childCount;

        /// <summary>
        /// Makes the host: a child object of <paramref name="parent"/> with a <see cref="UIDocument"/> of the shared panel settings. The list
        /// is found in <c>Start</c>, before the first frame, when the document's tree exists. Returns null, after logging one error that
        /// names the assets, when one is missing.
        /// </summary>
        /// <param name="parent">The object the host's object goes under.</param>
        /// <param name="queue">The toasts to show.</param>
        public static ToastHost? Create(Transform parent, ToastQueue queue)
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsPath);
            var layout = Resources.Load<VisualTreeAsset>(LayoutPath);
            if (panelSettings == null || layout == null)
            {
                Debug.LogError(
                    "ToastHost: cannot show toasts, an asset is missing under Assets/_Project/Resources: " +
                    $"{PanelSettingsPath} {(panelSettings == null ? "missing" : "found")}, " +
                    $"{LayoutPath} {(layout == null ? "missing" : "found")}.");
                return null;
            }

            // A UIDocument builds its tree when it is enabled, so it gets its settings while the object is still inactive.
            var hostObject = new GameObject("Toasts");
            hostObject.transform.SetParent(parent, false);
            hostObject.SetActive(false);
            var host = hostObject.AddComponent<ToastHost>();
            host._document = hostObject.AddComponent<UIDocument>();
            host._document.panelSettings = panelSettings;
            host._document.visualTreeAsset = layout;
            host._document.sortingOrder = SortingOrder;
            host._queue = queue;
            hostObject.SetActive(true);
            return host;
        }

        /// <summary>The time the queue counts in: whole milliseconds of unscaled play time (a double, so a long session keeps its precision), so a paused or sped-up game does not stretch a toast.</summary>
        public static long NowMilliseconds()
        {
            return (long)(Time.unscaledTimeAsDouble * 1000d);
        }

        /// <summary>Unity calls it before the first frame: the document's tree exists now, so the list is found and the queue is shown.</summary>
        private void Start()
        {
            VisualElement? root = Root;
            _list = root?.Q<VisualElement>(ListName);
            if (_queue == null || root == null || _list == null)
            {
                Debug.LogError($"ToastHost: the layout lacks '{ListName}'.");
                return;
            }

            root.RegisterCallback<GeometryChangedEvent>(_ => UiRoot.PadBySafeArea(root, BasePadding));
            _queue.Changed += Refresh;
            Refresh();
        }

        /// <summary>Unity calls it every frame: the toasts whose time is up go (the queue raises <c>Changed</c> only when one does).</summary>
        private void Update()
        {
            _queue?.Update(NowMilliseconds());
        }

        /// <summary>Unity calls it when the object is destroyed: the queue no longer reports to a screen that is gone.</summary>
        private void OnDestroy()
        {
            if (_queue != null)
            {
                _queue.Changed -= Refresh;
            }
        }

        /// <summary>Writes the queue's visible toasts into the list, oldest on top. It runs when the queue changes, not every frame.</summary>
        private void Refresh()
        {
            if (_queue == null || _list == null)
            {
                return;
            }

            _list.Clear();
            for (int index = 0; index < _queue.Visible.Count; index++)
            {
                Toast toast = _queue.Visible[index];
                var label = new Label(toast.Text) { pickingMode = PickingMode.Ignore };
                label.AddToClassList(ToastClass);
                label.AddToClassList(toast.Kind == ToastKind.Error ? ErrorClass : SuccessClass);
                _list.Add(label);
            }
        }
    }
}
