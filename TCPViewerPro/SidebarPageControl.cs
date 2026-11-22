namespace TCPViewerPro;

/// <summary>
/// Uses TabControl as a page container with only sidebar navigation visible.
/// Avoids a second row of native tabs and ensures the selected page fills the body.
/// </summary>
internal sealed class SidebarPageControl : TabControl
{
    private const int TCM_ADJUSTRECT = 0x1328;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == TCM_ADJUSTRECT && !DesignMode)
        {
            // Let the content occupy the entire client rectangle: no header.
            m.Result = (IntPtr)1;
            return;
        }
        base.WndProc(ref m);
    }
}
