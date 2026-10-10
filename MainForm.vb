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
    Private grpDetail As GroupBox
    Private _loadingDetail As Boolean
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
        ClientSize = New Size(760, 728)

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
        Dim gPhr = AddGroup("Phrases to look for (case / spacing / punctuation are ignored)", 128, 290)
        grid = New DataGridView With {
            .Left = 12, .Top = 22, .Width = 712, .Height = 108,
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
        AddHandler grid.CurrentCellChanged, Sub(s As Object, e As EventArgs) LoadDetailFromSelection()
        AddLabel(gPhr, "Type in the blank bottom row to add a phrase. Select a row and press Delete to remove it.", 12, 133, 700)
        _editable.Add(grid)

        ' Per-phrase sound / program, edited for the selected row.
        grpDetail = New GroupBox With {.Text = "Settings for the selected phrase", .Left = 12, .Top = 156, .Width = 712, .Height = 126}
        gPhr.Controls.Add(grpDetail)
        AddLabel(grpDetail, "Sound file:", 12, 24, 90)
        txtSound = AddText(grpDetail, 105, 22, 430)
        AddButton(grpDetail, "Browse...", 545, 20, 70, Sub(s, e) BrowseOpen(txtSound, "Audio|*.wav;*.mp3;*.wma;*.m4a|All files|*.*"))
        AddButton(grpDetail, "Test", 622, 20, 70, Sub(s, e) TestAction(Sub() ActionRunner.PlaySound(txtSound.Text)))
        AddLabel(grpDetail, "Run program:", 12, 52, 90)
        txtCommand = AddText(grpDetail, 105, 50, 430)
        AddButton(grpDetail, "Browse...", 545, 48, 70, Sub(s, e) BrowseOpen(txtCommand, "Programs / scripts|*.exe;*.bat;*.cmd;*.ps1;*.vbs;*.py|All files|*.*"))
        AddButton(grpDetail, "Test", 622, 48, 70, Sub(s, e) TestAction(Sub() ActionRunner.RunCommand(txtCommand.Text, txtArgs.Text, "(test)")))
        AddLabel(grpDetail, "Arguments:", 12, 76, 90)
        txtArgs = AddText(grpDetail, 105, 74, 430)
        AddLabel(grpDetail, "{phrase} and {time} are substituted", 545, 77, 165)
        AddLabel(grpDetail, "Flash colour:", 12, 104, 90)
        btnColor = AddButton(grpDetail, "", 105, 100, 60, AddressOf OnPickColor)
        AddButton(grpDetail, "Test flash", 175, 100, 90, Sub(s, e) FlashOverlay.Flash(btnColor.BackColor, CInt(nudFlashMs.Value)))
        For Each tb In {txtSound, txtCommand, txtArgs}
            AddHandler tb.TextChanged, Sub(s As Object, e As EventArgs) StoreDetailToSelection()
        Next
        grpDetail.Enabled = False
        _editable.Add(grpDetail)

        ' --- Actions ---
        Dim gAct = AddGroup("Flash duration and log file (shared by all phrases)", 424, 62)
        AddLabel(gAct, "Flash duration (ms):", 12, 26, 125)
        nudFlashMs = AddNud(gAct, 140, 24, 70, 50, 5000)
        _editable.Add(nudFlashMs)

        AddLabel(gAct, "Log file:", 400, 26, 60)
        txtLog = AddText(gAct, 462, 24, 160)
        AddButton(gAct, "...", 628, 22, 36, Sub(s, e) BrowseSave(txtLog, "Text / log|*.txt;*.log|All files|*.*"))
        AddButton(gAct, "Test", 670, 22, 56, Sub(s, e) TestAction(Sub() ActionRunner.WriteLog(txtLog.Text, "(test)", "Test entry from MessageTrigger")))
        _editable.Add(txtLog)

        ' --- Control row ---
        btnStart = AddButton(Me, "Start watching", 20, 498, 140, AddressOf OnStartStop)
        btnStart.Height = 34
        Dim btnSave = AddButton(Me, "Save settings", 170, 498, 120, Sub(s, e) SaveConfig(showStatus:=True))
        btnSave.Height = 34
        lblStatus = AddLabel(Me, "", 305, 507, 440)
        lblStatus.AutoEllipsis = True

        ' --- Last OCR output ---
        Dim gOcr = AddGroup("Last OCR result", 542, 172)
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
        nudFlashMs.Value = _cfg.FlashDurationMs
        txtLog.Text = _cfg.LogFile

        grid.Rows.Clear()
        For Each r In _cfg.Phrases
            Dim idx As Integer = grid.Rows.Add(r.Phrase, r.Flash, r.Sound, r.Log, r.Run)
            grid.Rows(idx).Tag = New PhraseRule With {.SoundFile = r.SoundFile, .CommandPath = r.CommandPath, .CommandArgs = r.CommandArgs, .FlashColor = r.FlashColor}
        Next
        LoadDetailFromSelection()
    End Sub

    ''' <summary>Per-phrase sound/program settings live on each grid row's Tag.</summary>
    Private Shared Function DetailOf(row As DataGridViewRow) As PhraseRule
        Dim r = TryCast(row.Tag, PhraseRule)
        If r Is Nothing Then
            r = New PhraseRule()
            row.Tag = r
        End If
        Return r
    End Function

    Private Sub LoadDetailFromSelection()
        Dim row As DataGridViewRow = grid.CurrentRow
        _loadingDetail = True
        Try
            If row Is Nothing OrElse row.IsNewRow Then
                grpDetail.Enabled = False
                txtSound.Text = ""
                txtCommand.Text = ""
                txtArgs.Text = ""
                btnColor.BackColor = Color.Red
            Else
                Dim d = DetailOf(row)
                grpDetail.Enabled = Not _watcher.IsRunning
                txtSound.Text = d.SoundFile
                txtCommand.Text = d.CommandPath
                txtArgs.Text = d.CommandArgs
                btnColor.BackColor = ActionRunner.ParseColor(d.FlashColor)
            End If
        Finally
            _loadingDetail = False
        End Try
    End Sub

    Private Sub StoreDetailToSelection()
        If _loadingDetail Then Return
        Dim row As DataGridViewRow = grid.CurrentRow
        If row Is Nothing OrElse row.IsNewRow Then Return
        Dim d = DetailOf(row)
        d.SoundFile = txtSound.Text.Trim()
        d.CommandPath = txtCommand.Text.Trim()
        d.CommandArgs = txtArgs.Text
        d.FlashColor = ColorTranslator.ToHtml(btnColor.BackColor)
    End Sub

    Private Sub ReadUiToConfig()
        grid.EndEdit()
        _cfg.PollIntervalMs = CInt(nudInterval.Value)
        _cfg.RearmAfterAbsentPolls = CInt(nudRearm.Value)
        _cfg.UpscaleFactor = CInt(nudUpscale.Value)
        _cfg.FlashDurationMs = CInt(nudFlashMs.Value)
        _cfg.LogFile = txtLog.Text.Trim()

        _cfg.Phrases = New List(Of PhraseRule)()
        For Each row As DataGridViewRow In grid.Rows
            If row.IsNewRow Then Continue For
            Dim phrase As String = Convert.ToString(row.Cells(0).Value)
            If String.IsNullOrWhiteSpace(phrase) Then Continue For
            Dim d = DetailOf(row)
            _cfg.Phrases.Add(New PhraseRule With {
                .Phrase = phrase.Trim(),
                .Flash = CellChecked(row, 1),
                .Sound = CellChecked(row, 2),
                .Log = CellChecked(row, 3),
                .Run = CellChecked(row, 4),
                .SoundFile = d.SoundFile,
                .CommandPath = d.CommandPath,
                .CommandArgs = d.CommandArgs,
                .FlashColor = d.FlashColor
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
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                btnColor.BackColor = dlg.Color
                StoreDetailToSelection()
            End If
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
        If enabled Then LoadDetailFromSelection() ' re-enables the detail box only if a real row is selected
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
