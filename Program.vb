Module Program

    <STAThread>
    Public Sub Main()
        ' Physical-pixel coordinates so the capture rectangle matches what is on screen.
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub

End Module
