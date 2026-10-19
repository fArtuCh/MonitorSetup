namespace MonitorSetup.App.UI;

/// <summary>Asks the user whether to keep a display change; no answer means no.</summary>
internal interface IChangeConfirmation
{
    bool ConfirmKeepChanges();
}

internal sealed class DialogChangeConfirmation : IChangeConfirmation
{
    public bool ConfirmKeepChanges()
    {
        using var dialog = new ConfirmChangesForm();
        return dialog.ShowDialog(Form.ActiveForm) == DialogResult.OK;
    }
}
