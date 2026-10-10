Imports System.IO
Imports System.ComponentModel

''' <summary>Setup + control screen. Built in code (no designer file) to keep the project small.</summary>
Public Class MainForm
    Inherits Form

    Private _cfg As AppConfig
    Private ReadOnly _watcher As New ScreenWatcher()
    Private ReadOnly _frame As New FrameOverlay()
    Private _frameVisible As Boolean = True

    Private lblRegion As Label
    Private btnSelect As Button
    Private btnFrame As Button
    Private nudInterval As NumericUpDown
    Private nudRearm As NumericUpDown
    Private nudUpscale As NumericUpDown
    Private grid As DataGridView
    Private btnColor As Button
    Private nudFlashMs As NumericUpDown
    Private txtSound As TextBox
    Private txtLog As TextBox
    Private txtCommand As TextBox
    Private txtArgs As TextBox
    Private btnStart As Button
    Private lblStatus As Label
    Private txtOcr As TextBox
    Private ReadOnly _editable As New List(Of Control)

    Public Sub New()
        Text = "MessageTrigger - screen OCR phrase watcher"
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        StartPosition = FormStartPosition.CenterScreen
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New Size(760, 766)

        BuildUi()

        AddHandler _watcher.TextRead, AddressOf OnTextRead
        AddHandler _watcher.Triggered, AddressOf OnTriggered
        AddHandler _watcher.ErrorOccurred, Sub(msg As String) SetStatus("Error: " & msg)
        AddHandler _watcher.Stopped, AddressOf OnWatcherStopped

        _cfg = AppConfig.Load()
        ApplyConfigToUi()
        UpdateRegionLabel()
        If _cfg.HasRegion Then _frame.ShowAround(_cfg.Region)
        SetStatus(If(File.Exists(AppConfig.ConfigPath), "Loaded settings from " & AppConfig.ConfigPath, "No saved settings yet - defaults loaded."))
    End Sub

#Region "UI construction"

    Private Sub BuildUi()
        ' --- Capture region ---
        Dim gRegion = AddGroup("Capture region", 8, 54)
        lblRegion = AddLabel(gRegion, "", 12, 22, 410)
        btnSelect = AddButton(gRegion, "Select region...", 430, 18, 140, AddressOf OnSelectRegion)
        btnFrame = AddButton(gRegion, "Hide frame", 580, 18, 140, AddressOf OnToggleFrame)
        _editable.Add(btnSelect)

        ' --- Polling ---
        Dim gPoll = AddGroup("Polling / OCR", 68, 54)
        AddLabel(gPoll, "Poll interval (ms):", 12, 22, 120)
        nudInterval = AddNud(gPoll, 135, 20, 80, 100, 60000)
        AddLabel(gPoll, "Re-arm after absent polls:", 240, 22, 160)
        nudRearm = AddNud(gPoll, 405, 20, 60, 1, 1000)
        AddLabel(gPoll, "Upscale (x):", 500, 22, 80)
        nudUpscale = AddNud(gPoll, 585, 20, 50, 1, 4)
        _editable.AddRange({nudInterval, nudRearm, nudUpscale})

        ' --- Phrases ---
        Dim gPhr = AddGroup("Phrases to look for (case / spacing / punctuation are ignored)", 128, 206)
        grid = New DataGridView With {
            .Left = 12, .Top = 22, .Width = 712, .Height = 150,
            .AllowUserToResizeRows = False,
            .RowHeadersVisible = True,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            .BackgroundColor = SystemColors.Window,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect
        }
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.HeaderText = "Phrase", .FillWeight = 300})
        For Each h In {"Flash", "Sound", "Log", "Run"}
            grid.Columns.Add(New DataGridViewCheckBoxColumn With {.HeaderText = h, .FillWeight = 40})
        Next
        AddHandler grid.DefaultValuesNeeded, Sub(s As Object, e As DataGridViewRowEventArgs)
                                                 e.Row.Cells(1).Value = True
                                                 e.Row.Cells(2).Value = True
                                                 e.Row.Cells(3).Value = True
                                                 e.Row.Cells(4).Value = False
                                             End Sub
        gPhr.Controls.Add(grid)
        AddLabel(gPhr, "Type in the blank bottom row to add a phrase. Select a row and press Delete to remove it.", 12, 175, 700)
        _editable.Add(grid)

        ' --- Actions ---
        Dim gAct = AddGroup("Actions", 340, 190)
        AddLabel(gAct, "Flash colour:", 12, 26, 90)
        btnColor = AddButton(gAct, "", 105, 22, 60, AddressOf OnPickColor)
        AddLabel(gAct, "Duration (ms):", 190, 26, 100)
        nudFlashMs = AddNud(gAct, 295, 24, 70, 50, 5000)
        AddButton(gAct, "Test flash", 385, 22, 100, Sub(s, e) FlashOverlay.Flash(btnColor.BackColor, CInt(nudFlashMs.Value)))
        _editable.AddRange({btnColor, nudFlashMs})

        AddLabel(gAct, "Sound file:", 12, 60, 90)
        txtSound = AddText(gAct, 105, 58, 440)
        AddButton(gAct, "Browse...", 555, 56, 80, Sub(s, e) BrowseOpen(txtSound, "Audio|*.wav;*.mp3;*.wma;*.m4a|All files|*.*"))
        AddButton(gAct, "Test", 642, 56, 70, Sub(s, e) TestAction(Sub() ActionRunner.PlaySound(txtSound.Text)))
        AddLabel(gAct, "(blank = system beep)", 105, 82, 300)

        AddLabel(gAct, "Log file:", 12, 108, 90)
        txtLog = AddText(gAct, 105, 106, 440)
        AddButton(gAct, "Browse...", 555, 104, 80, Sub(s, e) BrowseSave(txtLog, "Text / log|*.txt;*.log|All files|*.*"))
        AddButton(gAct, "Test", 642, 104, 70, Sub(s, e) TestAction(Sub() ActionRunner.WriteLog(txtLog.Text, "(test)", "Test entry from MessageTrigger")))

        AddLabel(gAct, "Run program:", 12, 140, 90)
        txtCommand = AddText(gAct, 105, 138, 440)
        AddButton(gAct, "Browse...", 555, 136, 80, Sub(s, e) BrowseOpen(txtCommand, "Programs / scripts|*.exe;*.bat;*.cmd;*.ps1;*.vbs;*.py|All files|*.*"))
        AddButton(gAct, "Test", 642, 136, 70, Sub(s, e) TestAction(Sub() ActionRunner.RunCommand(txtCommand.Text, txtArgs.Text, "(test)")))
        AddLabel(gAct, "Arguments:", 12, 166, 90)
        txtArgs = AddText(gAct, 105, 164, 440)
        AddLabel(gAct, "{phrase} and {time} are substituted", 552, 168, 180)
        _editable.AddRange({txtSound, txtLog, txtCommand, txtArgs})

        ' --- Control row ---
        btnStart = AddButton(Me, "Start watching", 20, 542, 140, AddressOf OnStartStop)
        btnStart.Height = 34
        Dim btnSave = AddButton(Me, "Save settings", 170, 542, 120, Sub(s, e) SaveConfig(showStatus:=True))
        btnSave.Height = 34
        lblStatus = AddLabel(Me, "", 305, 551, 440)
        lblStatus.AutoEllipsis = True

        ' --- Last OCR output ---
        Dim gOcr = AddGroup("Last OCR result", 586, 172)
        txtOcr = New TextBox With {
            .Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical,
            .Left = 12, .Top = 22, .Width = 712, .Height = 140
        }
        gOcr.Controls.Add(txtOcr)
    End Sub

    Private Function AddGroup(title As String, top As Integer, height As Integer) As GroupBox
        Dim g As New GroupBox With {.Text = title, .Left = 12, .Top = top, .Width = 736, .Height = height}
        Controls.Add(g)
        Return g
    End Function

    Private Shared Function AddLabel(parent As Control, text As String, x As Integer, y As Integer, w As Integer) As Label
        Dim l As New Label With {.Text = text, .Left = x, .Top = y, .Width = w, .Height = 20}
        parent.Controls.Add(l)
        Return l
    End Function

    Private Shared Function AddText(parent As Control, x As Integer, y As Integer, w As Integer) As TextBox
        Dim t As New TextBox With {.Left = x, .Top = y, .Width = w}
        parent.Controls.Add(t)
        Return t
    End Function

    Private Shared Function AddNud(parent As Control, x As Integer, y As Integer, w As Integer, min As Integer, max As Integer) As NumericUpDown
        Dim n As New NumericUpDown With {.Left = x, .Top = y, .Width = w, .Minimum = min, .Maximum = max, .Value = min}
        parent.Controls.Add(n)
        Return n
    End Function

    Private Shared Function AddButton(parent As Control, text As String, x As Integer, y As Integer, w As Integer, handler As EventHandler) As Button
        Dim b As New Button With {.Text = text, .Left = x, .Top = y, .Width = w, .Height = 28}
        AddHandler b.Click, handler
        parent.Controls.Add(b)
        Return b
    End Function

#End Region

#Region "Config <-> UI"

    Private Sub ApplyConfigToUi()
        nudInterval.Value = _cfg.PollIntervalMs
        nudRearm.Value = _cfg.RearmAfterAbsentPolls
        nudUpscale.Value = _cfg.UpscaleFactor
        btnColor.BackColor = ActionRunner.ParseColor(_cfg.FlashColor)
        nudFlashMs.Value = _cfg.FlashDurationMs
        txtSound.Text = _cfg.SoundFile
        txtLog.Text = _cfg.LogFile
        txtCommand.Text = _cfg.CommandPath
        txtArgs.Text = _cfg.CommandArgs

        grid.Rows.Clear()
        For Each r In _cfg.Phrases
            grid.Rows.Add(r.Phrase, r.Flash, r.Sound, r.Log, r.Run)
        Next
    End Sub

    Private Sub ReadUiToConfig()
        grid.EndEdit()
        _cfg.PollIntervalMs = CInt(nudInterval.Value)
        _cfg.RearmAfterAbsentPolls = CInt(nudRearm.Value)
        _cfg.UpscaleFactor = CInt(nudUpscale.Value)
        _cfg.FlashColor = ColorTranslator.ToHtml(btnColor.BackColor)
        _cfg.FlashDurationMs = CInt(nudFlashMs.Value)
        _cfg.SoundFile = txtSound.Text.Trim()
        _cfg.LogFile = txtLog.Text.Trim()
        _cfg.CommandPath = txtCommand.Text.Trim()
        _cfg.CommandArgs = txtArgs.Text

        _cfg.Phrases = New List(Of PhraseRule)()
        For Each row As DataGridViewRow In grid.Rows
            If row.IsNewRow Then Continue For
            Dim phrase As String = Convert.ToString(row.Cells(0).Value)
            If String.IsNullOrWhiteSpace(phrase) Then Continue For
            _cfg.Phrases.Add(New PhraseRule With {
                .Phrase = phrase.Trim(),
                .Flash = CellChecked(row, 1),
                .Sound = CellChecked(row, 2),
                .Log = CellChecked(row, 3),
                .Run = CellChecked(row, 4)
            })
        Next
    End Sub

    Private Shared Function CellChecked(row As DataGridViewRow, index As Integer) As Boolean
        Dim v As Object = row.Cells(index).Value
        Return v IsNot Nothing AndAlso Convert.ToBoolean(v)
    End Function

    Private Sub SaveConfig(showStatus As Boolean)
        Try
            ReadUiToConfig()
            _cfg.Save()
            If showStatus Then SetStatus("Settings saved to " & AppConfig.ConfigPath)
        Catch ex As Exception
            SetStatus("Could not save settings: " & ex.Message)
        End Try
    End Sub

#End Region

#Region "Event handlers"

    Private Async Sub OnSelectRegion(sender As Object, e As EventArgs)
        _frame.Hide()
        WindowState = FormWindowState.Minimized
        Await Task.Delay(250) ' let the window actually disappear before dimming the screens
        Dim picked As Rectangle? = Await SelectionOverlay.PickAsync()
        WindowState = FormWindowState.Normal
        Activate()

        If picked.HasValue Then
            _cfg.Region = picked.Value
            UpdateRegionLabel()
            SaveConfig(showStatus:=False)
            SetStatus("Region set. Settings saved.")
        End If
        If _cfg.HasRegion AndAlso _frameVisible Then _frame.ShowAround(_cfg.Region)
    End Sub

    Private Sub OnToggleFrame(sender As Object, e As EventArgs)
        _frameVisible = Not _frameVisible
        btnFrame.Text = If(_frameVisible, "Hide frame", "Show frame")
        If _frameVisible Then
            If _cfg.HasRegion Then _frame.ShowAround(_cfg.Region)
        Else
            _frame.Hide()
        End If
    End Sub

    Private Sub OnPickColor(sender As Object, e As EventArgs)
        Using dlg As New ColorDialog With {.Color = btnColor.BackColor}
            If dlg.ShowDialog(Me) = DialogResult.OK Then btnColor.BackColor = dlg.Color
        End Using
    End Sub

    Private Sub OnStartStop(sender As Object, e As EventArgs)
        If _watcher.IsRunning Then
            _watcher.Stop()
            Return
        End If

        ReadUiToConfig()
        If Not _cfg.HasRegion Then
            MessageBox.Show(Me, "Select a capture region first.", "MessageTrigger", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If _cfg.Phrases.Count = 0 Then
            MessageBox.Show(Me, "Add at least one phrase first.", "MessageTrigger", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        SaveConfig(showStatus:=False)
        SetEditable(False)
        btnStart.Text = "Stop watching"
        SetStatus("Watching...")
        _watcher.Start(_cfg)
    End Sub

    Private Sub OnWatcherStopped()
        SetEditable(True)
        btnStart.Text = "Start watching"
        SetStatus("Stopped.")
    End Sub

    Private Sub OnTextRead(text As String)
        txtOcr.Text = text.Replace(vbLf, vbCrLf)
    End Sub

    Private Sub OnTriggered(rule As PhraseRule, context As String)
        SetStatus($"Triggered '{rule.Phrase}'")
        ActionRunner.Execute(_cfg, rule, context, Sub(msg) SetStatus(msg))
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not _watcher.IsRunning Then SaveConfig(showStatus:=False)
        _watcher.Stop()
        _frame.Close()
        MyBase.OnFormClosing(e)
    End Sub

#End Region

#Region "Helpers"

    Private Sub SetEditable(enabled As Boolean)
        For Each c In _editable
            c.Enabled = enabled
        Next
    End Sub

    Private Sub UpdateRegionLabel()
        If _cfg.HasRegion Then
            Dim r = _cfg.Region
            lblRegion.Text = $"X={r.X}  Y={r.Y}  Width={r.Width}  Height={r.Height}"
        Else
            lblRegion.Text = "No region selected."
        End If
    End Sub

    Private Sub SetStatus(message As String)
        lblStatus.Text = $"[{Now:HH:mm:ss}] {message}"
    End Sub

    Private Sub TestAction(act As Action)
        Try
            act()
            SetStatus("Test action ran.")
        Catch ex As Exception
            SetStatus("Test failed: " & ex.Message)
        End Try
    End Sub

    Private Sub BrowseOpen(target As TextBox, filter As String)
        Using dlg As New OpenFileDialog With {.Filter = filter, .CheckFileExists = True}
            If dlg.ShowDialog(Me) = DialogResult.OK Then target.Text = dlg.FileName
        End Using
    End Sub

    Private Sub BrowseSave(target As TextBox, filter As String)
        Using dlg As New SaveFileDialog With {.Filter = filter, .OverwritePrompt = False, .CheckFileExists = False}
            If dlg.ShowDialog(Me) = DialogResult.OK Then target.Text = dlg.FileName
        End Using
    End Sub

#End Region

End Class
