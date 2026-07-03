using Eto.Drawing;
using Eto.Forms;

namespace DB3DFloora
{
    /// <summary>「常用造型」命名用的小型 modal 對話框：一個文字輸入框加確定／取消。</summary>
    public class PresetNameDialog : Dialog<string>
    {
        private readonly TextBox _nameBox;

        public PresetNameDialog(Window owner, string defaultName)
        {
            Title = Strings.T("dlg.presetName");
            // 不設 Topmost：這個對話框的 owner（主視窗 FlooraForm）本身也是 Topmost，
            // 兩個各自 Topmost 的視窗會互搶最上層，導致這個小視窗有時反而被蓋到主視窗後面。
            // Modal 對話框只要 Owner 設對，原生就會正確浮在 owner 上面，不需要再額外設 Topmost。
            Resizable = false;
            BackgroundColor = UiStyle.CBg;
            Padding = new Padding(10);
            if (owner != null)
                Owner = owner;

            var promptLabel = UiStyle.MakeLabel(Strings.T("dlg.presetNamePrompt"), null, null, TextAlignment.Left);
            _nameBox = new TextBox { Text = defaultName ?? "", Width = 220 };

            var okBtn = UiStyle.MakeButton(Strings.T("btn.ok"), true);
            okBtn.Click += (s, e) => Close(_nameBox.Text?.Trim());
            var cancelBtn = UiStyle.MakeButton(Strings.T("btn.cancel"));
            cancelBtn.Click += (s, e) => Close(null);

            var outer = UiStyle.VStack();
            UiStyle.AddRow(outer, promptLabel);
            UiStyle.AddRow(outer, _nameBox);
            UiStyle.AddRow(outer, UiStyle.HRow(null, cancelBtn, okBtn));

            Content = outer;
            DefaultButton = okBtn;
            AbortButton = cancelBtn;
        }

        /// <summary>顯示對話框並回傳使用者輸入的名稱（trim 過）；取消或輸入空字串回傳 null。</summary>
        public static string Ask(Window owner, string defaultName = null)
        {
            var dlg = new PresetNameDialog(owner, defaultName);
            var result = dlg.ShowModal(owner);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}
