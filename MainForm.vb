Imports System.IO

''' <summary>Setup + control screen. Built in code (no designer file) to keep the project small.</summary>
Public Class MainForm
    Inherits Form

    Private Const BaseTitle As String = "MessageTrigger - screen OCR phrase watcher"
    Private Const BannerHeight As Integer = 32

    Private _cfg As AppConfig
    Private ReadOnly _watcher As New ScreenWatcher()
    Private ReadOnly _frame As New FrameOverlay()
    Private _frameVisible As Boolean = True

    ' Settings are applied to the running watcher immediately; the file is written after a short quiet period.
    Private ReadOnly _saveTimer As New System.Windows.Forms.Timer With {.Interval = 600}
    Private ReadOnly _pulseTimer As New System.Windows.Forms.Timer With {.Interval = 250}
    Private _suspendLive As Boolean

    Private _stopReason As String
    Private _lastPassFailed As Boolean
    Private _lastFailure As String = ""
    Private _lastPollTime As DateTime
    Private _pulsing As Boolean

    Private pnlBanner As Panel
    Private lblBanner As Label
    Private lblRegion As Label
    Private btnSelect As Button
    Private btnFrame As Button
    Private nudInterval As NumericUpDown
    Private nudRearm As NumericUpDown
    Private nudUpscale As NumericUpDown
    Private grid As DataGridView
    Private btnColor As Button
    Private nudFlashMs As NumericUpDown
    Private chkAutoStart As CheckBox
    Private grpDetail As GroupBox
    Private _loadingDetail As Boolean
    Private txtSound As TextBox
    Private txtLog As TextBox
    Private txtCommand As TextBox
    Private txtArgs As TextBox
    Private btnStart As Button
    Private lblStatus As Label
    Private txtOcr As TextBox

    Public Sub New()
        Text = BaseTitle
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        StartPosition = FormStartPosition.CenterScreen
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New Size(760, 700)

        BuildUi()

        AddHandler _watcher.TextRead, AddressOf OnTextRead
        AddHandler _watcher.Triggered, AddressOf OnTriggered
        AddHandler _watcher.ErrorOccurred, AddressOf OnWatcherError
        AddHandler _watcher.PollCompleted, AddressOf OnPollCompleted
        AddHandler _watcher.Stopped, AddressOf OnWatcherStopped
        AddHandler _saveTimer.Tick, AddressOf OnSaveTimerTick
        AddHandler _pulseTimer.Tick, Sub(s As Object, e As EventArgs)
                                         _pulseTimer.Stop()
                                         _pulsing = False
                                         UpdateBanner()
                                     End Sub

        _cfg = AppConfig.Load()
        ApplyConfigToUi()
        UpdateRegionLabel()
        If _cfg.HasRegion Then _frame.ShowAround(_cfg.Region)
        UpdateBanner()
        SetStatus(If(File.Exists(AppConfig.ConfigPath), "Loaded settings from " & AppConfig.ConfigPath, "No saved settings yet - defaults loaded."))
    End Sub

#Region "UI construction"

    Private Sub BuildUi()
        ' --- Always-visible watching indicator ---
        pnlBanner = New Panel With {.Left = 0, .Top = 0, .Width = 760, .Height = BannerHeight}
        lblBanner = New Label With {
            .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .AutoEllipsis = True,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold), .Padding = New Padding(12, 0, 0, 0)
        }
        pnlBanner.Controls.Add(lblBanner)
        Controls.Add(pnlBanner)

        ' --- Capture region ---
        Dim gRegion = AddGroup("Capture region", 8, 54)
        lblRegion = AddLabel(gRegion, "", 12, 22, 410)
        btnSelect = AddButton(gRegion, "Select region...", 430, 18, 140, AddressOf OnSelectRegion)
        btnFrame = AddButton(gRegion, "Hide frame", 580, 18, 140, AddressOf OnToggleFrame)

        ' --- Polling ---
        Dim gPoll = AddGroup("Polling / OCR (changes apply immediately)", 68, 54)
        AddLabel(gPoll, "Poll interval (ms):", 12, 22, 120)
        nudInterval = AddNud(gPoll, 135, 20, 80, 100, 60000)
        AddLabel(gPoll, "Re-arm after absent polls:", 240, 22, 160)
        nudRearm = AddNud(gPoll, 405, 20, 60, 1, 1000)
        AddLabel(gPoll, "Upscale (x):", 500, 22, 80)
        nudUpscale = AddNud(gPoll, 585, 20, 50, 1, 4)
        For Each n In {nudInterval, nudRearm, nudUpscale}
            AddHandler n.ValueChanged, Sub(s As Object, e As EventArgs) OnSettingChanged()
        Next

        ' --- Phrases ---
        Dim gPhr = AddGroup("Phrases to look for (case / spacing / punctuation are ignored)", 128, 306)
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
        ' Commit a ticked checkbox straight away so it applies live instead of on focus change.
        AddHandler grid.CurrentCellDirtyStateChanged, Sub(s As Object, e As EventArgs)
                                                          If grid.IsCurrentCellDirty AndAlso TypeOf grid.CurrentCell Is DataGridViewCheckBoxCell Then
                                                              grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
                                                          End If
                                                      End Sub
        AddHandler grid.CellValueChanged, Sub(s As Object, e As DataGridViewCellEventArgs) OnSettingChanged()
        AddHandler grid.UserDeletedRow, Sub(s As Object, e As DataGridViewRowEventArgs) OnSettingChanged()
        AddLabel(gPhr, "Type in the blank bottom row to add a phrase. Select a row and press Delete to remove it.", 12, 133, 700)

        ' Per-phrase sound / program, edited for the selected row.
        grpDetail = New GroupBox With {.Text = "Settings for the selected phrase", .Left = 12, .Top = 156, .Width = 712, .Height = 142}
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
        AddLabel(grpDetail, "{phrase} and {time} are substituted in the arguments", 545, 80, 160).Height = 34
        AddLabel(grpDetail, "Flash colour:", 12, 106, 90)
        btnColor = AddButton(grpDetail, "", 105, 102, 60, AddressOf OnPickColor)
        AddButton(grpDetail, "Test flash", 175, 102, 90, Sub(s, e) FlashOverlay.Flash(btnColor.BackColor, CInt(nudFlashMs.Value)))
        For Each tb In {txtSound, txtCommand, txtArgs}
            AddHandler tb.TextChanged, Sub(s As Object, e As EventArgs)
                                           StoreDetailToSelection()
                                           OnSettingChanged()
                                       End Sub
        Next
        grpDetail.Enabled = False

        ' --- Actions ---
        Dim gAct = AddGroup("Flash length and log file (shared by all phrases)", 440, 62)
        AddLabel(gAct, "Flash length (ms):", 12, 26, 125)
        nudFlashMs = AddNud(gAct, 140, 24, 70, 200, 10000)
        AddHandler nudFlashMs.ValueChanged, Sub(s As Object, e As EventArgs) OnSettingChanged()

        AddLabel(gAct, "Log file:", 235, 26, 55)
        txtLog = AddText(gAct, 292, 24, 190)
        AddHandler txtLog.TextChanged, Sub(s As Object, e As EventArgs) OnSettingChanged()
        AddButton(gAct, "...", 488, 22, 36, Sub(s, e) BrowseSave(txtLog, "Text / log|*.txt;*.log|All files|*.*"))
        AddButton(gAct, "Test", 530, 22, 56, Sub(s, e) TestAction(Sub() ActionRunner.WriteLog(txtLog.Text, "(test)", "Test entry from MessageTrigger")))
        AddButton(gAct, "Edit", 592, 22, 56, AddressOf OnEditLog)

        ' --- Control row ---
        btnStart = AddButton(Me, "Start watching", 20, 514 + BannerHeight, 140, AddressOf OnStartStop)
        btnStart.Height = 34
        Dim btnSave = AddButton(Me, "Save settings", 170, 514 + BannerHeight, 120, Sub(s, e) SaveConfig(showStatus:=True))
        btnSave.Height = 34
        chkAutoStart = New CheckBox With {.Text = "Start watching when the app opens", .Left = 305, .Top = 521 + BannerHeight, .Width = 270, .Height = 22}
        AddHandler chkAutoStart.CheckedChanged, Sub(s As Object, e As EventArgs) OnSettingChanged()
        Controls.Add(chkAutoStart)
        lblStatus = AddLabel(Me, "", 20, 553 + BannerHeight, 725)
        lblStatus.AutoEllipsis = True

        ' --- Last OCR output ---
        Dim gOcr = AddGroup("Last OCR result", 576, 88)
        txtOcr = New TextBox With {
            .Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical,
            .Left = 12, .Top = 20, .Width = 712, .Height = 60
        }
        gOcr.Controls.Add(txtOcr)
    End Sub

    Private Function AddGroup(title As String, top As Integer, height As Integer) As GroupBox
        Dim g As New GroupBox With {.Text = title, .Left = 12, .Top = top + BannerHeight, .Width = 736, .Height = height}
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
        _suspendLive = True
        Try
            nudInterval.Value = _cfg.PollIntervalMs
            nudRearm.Value = _cfg.RearmAfterAbsentPolls
            nudUpscale.Value = _cfg.UpscaleFactor
            nudFlashMs.Value = _cfg.FlashDurationMs
            txtLog.Text = _cfg.LogFile
            chkAutoStart.Checked = _cfg.StartWatchingOnStartup

            grid.Rows.Clear()
            For Each r In _cfg.Phrases
                Dim idx As Integer = grid.Rows.Add(r.Phrase, r.Flash, r.Sound, r.Log, r.Run)
                grid.Rows(idx).Tag = New PhraseRule With {.SoundFile = r.SoundFile, .CommandPath = r.CommandPath, .CommandArgs = r.CommandArgs, .FlashColor = r.FlashColor}
            Next
        Finally
            _suspendLive = False
        End Try
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
                grpDetail.Enabled = True
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

    ' commitEdit:=False leaves a half-typed grid cell alone (used for live/background updates).
    Private Sub ReadUiToConfig(Optional commitEdit As Boolean = True)
        If commitEdit Then grid.EndEdit()
        _cfg.PollIntervalMs = CInt(nudInterval.Value)
        _cfg.RearmAfterAbsentPolls = CInt(nudRearm.Value)
        _cfg.UpscaleFactor = CInt(nudUpscale.Value)
        _cfg.FlashDurationMs = CInt(nudFlashMs.Value)
        _cfg.LogFile = txtLog.Text.Trim()
        _cfg.StartWatchingOnStartup = chkAutoStart.Checked

        Dim phrases As New List(Of PhraseRule)()
        For Each row As DataGridViewRow In grid.Rows
            If row.IsNewRow Then Continue For
            Dim phrase As String = Convert.ToString(row.Cells(0).Value)
            If String.IsNullOrWhiteSpace(phrase) Then Continue For
            Dim d = DetailOf(row)
            phrases.Add(New PhraseRule With {
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
        _cfg.Phrases = phrases ' replaced as a whole so the running watcher never sees a half-built list
    End Sub

    Private Shared Function CellChecked(row As DataGridViewRow, index As Integer) As Boolean
        Dim v As Object = row.Cells(index).Value
        Return v IsNot Nothing AndAlso Convert.ToBoolean(v)
    End Function

    Private Sub SaveConfig(showStatus As Boolean, Optional commitEdit As Boolean = True)
        Try
            ReadUiToConfig(commitEdit)
            _cfg.Save()
            If showStatus Then SetStatus("Settings saved to " & AppConfig.ConfigPath)
        Catch ex As Exception
            SetStatus("Could not save settings: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Any setting edited: push it into the live config now, persist shortly after.</summary>
    Private Sub OnSettingChanged()
        If _suspendLive OrElse _cfg Is Nothing Then Return
        Dim oldInterval As Integer = _cfg.PollIntervalMs
        ReadUiToConfig(commitEdit:=False)
        If _watcher.IsRunning AndAlso _cfg.PollIntervalMs <> oldInterval Then _watcher.Nudge()
        If _watcher.IsRunning Then UpdateBanner()
        _saveTimer.Stop()
        _saveTimer.Start()
    End Sub

    Private Sub OnSaveTimerTick(sender As Object, e As EventArgs)
        _saveTimer.Stop()
        SaveConfig(showStatus:=False, commitEdit:=False)
        If _watcher.IsRunning Then SetStatus("Settings applied live and saved.")
    End Sub

#End Region

#Region "Event handlers"

    Private Async Sub OnSelectRegion(sender As Object, e As EventArgs)
        ' Keep the poll loop alive but stop it OCR'ing the dimmed selection screens.
        _watcher.Paused = True
        UpdateBanner()
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
            SetStatus("Region set and applied. Settings saved.")
        End If
        If _cfg.HasRegion AndAlso _frameVisible Then _frame.ShowAround(_cfg.Region)
        _watcher.Paused = False
        UpdateBanner()
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
                OnSettingChanged()
            End If
        End Using
    End Sub

    Private Sub OnStartStop(sender As Object, e As EventArgs)
        If _watcher.IsRunning Then
            _watcher.Stop()
            Return
        End If

        StartWatching(interactive:=True)
    End Sub

    ' Validates and starts polling. When not interactive (auto-start) problems go to the status line, not a dialog.
    Private Sub StartWatching(interactive As Boolean)
        ReadUiToConfig()
        Dim problem As String = Nothing
        If Not _cfg.HasRegion Then
            problem = "Select a capture region first."
        ElseIf _cfg.Phrases.Count = 0 Then
            problem = "Add at least one phrase first."
        End If
        If problem IsNot Nothing Then
            If interactive Then
                MessageBox.Show(Me, problem, "MessageTrigger", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Else
                SetStatus("Auto-start skipped: " & problem)
            End If
            Return
        End If

        SaveConfig(showStatus:=False)
        _stopReason = Nothing
        _lastPassFailed = False
        _lastPollTime = DateTime.MinValue
        btnStart.Text = "Stop watching"
        SetStatus("Watching started.")
        _watcher.Start(_cfg)
        UpdateBanner()
    End Sub

    Private Sub OnWatcherError(message As String)
        _lastPassFailed = True
        _lastFailure = message
        SetStatus("Error: " & message)
        UpdateBanner()
    End Sub

    Private Sub OnWatcherStopped(failure As String)
        _stopReason = failure
        btnStart.Text = "Start watching"
        ' Keep the failure text visible rather than replacing it with a generic message.
        SetStatus(If(failure Is Nothing, "Stopped.", "Stopped - " & failure))
        UpdateBanner()
    End Sub

    Private Sub OnPollCompleted(pollNumber As Integer)
        _lastPollTime = DateTime.Now
        _pulsing = True
        _pulseTimer.Stop()
        _pulseTimer.Start()
        UpdateBanner()
    End Sub

    Private Sub OnTextRead(text As String)
        _lastPassFailed = False
        txtOcr.Text = text.Replace(vbLf, vbCrLf)
    End Sub

    Private Sub OnTriggered(rule As PhraseRule, context As String)
        SetStatus($"Triggered '{rule.Phrase}'")
        ActionRunner.Execute(_cfg, rule, context, Sub(msg) SetStatus(msg))
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        If _cfg.StartWatchingOnStartup Then StartWatching(interactive:=False)
    End Sub

    Private Sub OnEditLog(sender As Object, e As EventArgs)
        Dim logPath As String = txtLog.Text.Trim()
        If logPath.Length = 0 Then
            SetStatus("No log file configured.")
            Return
        End If
        Try
            Dim full As String = Path.GetFullPath(logPath)
            Dim dir As String = Path.GetDirectoryName(full)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            If Not File.Exists(full) Then File.WriteAllText(full, "")
            Using Process.Start(New ProcessStartInfo("notepad.exe", ControlChars.Quote & full & ControlChars.Quote) With {.UseShellExecute = False})
            End Using
        Catch ex As Exception
            SetStatus("Could not open log in Notepad: " & ex.Message)
        End Try
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        _saveTimer.Stop()
        _pulseTimer.Stop()
        SaveConfig(showStatus:=False) ' live edits are always persisted, even while watching
        _watcher.Stop()
        _frame.Close()
        MyBase.OnFormClosing(e)
    End Sub

#End Region

#Region "Helpers"

    ' The banner is the primary "is it running?" indicator: colour + text + a pulse on every scan.
    Private Sub UpdateBanner()
        Dim bg As Color
        Dim msg As String
        Dim dot As String = ChrW(&H25CF) & "  "
        Dim square As String = ChrW(&H25A0) & "  "

        If Not _watcher.IsRunning Then
            If _stopReason Is Nothing Then
                bg = Color.FromArgb(96, 96, 96)
                msg = square & "NOT WATCHING  -  press Start watching to begin"
            Else
                bg = Color.FromArgb(176, 32, 32)
                msg = square & "STOPPED  -  " & _stopReason
            End If
            Text = BaseTitle
        ElseIf _watcher.Paused Then
            bg = Color.FromArgb(60, 90, 170)
            msg = dot & "PAUSED while you select a region"
            Text = BaseTitle & "  [PAUSED]"
        Else
            Dim last As String = If(_lastPollTime = DateTime.MinValue, "scanning...", "last scan " & _lastPollTime.ToString("HH:mm:ss"))
            If _lastPassFailed Then
                bg = Color.FromArgb(190, 110, 0)
                msg = dot & "WATCHING  -  last scan FAILED: " & _lastFailure
            Else
                bg = If(_pulsing, Color.FromArgb(76, 200, 96), Color.FromArgb(28, 120, 48))
                msg = dot & $"WATCHING  -  scan #{_watcher.PollCount}  -  {last}  -  every {_cfg.PollIntervalMs} ms"
            End If
            Text = BaseTitle & "  [WATCHING]"
        End If

        pnlBanner.BackColor = bg
        lblBanner.BackColor = bg
        lblBanner.ForeColor = Color.White
        lblBanner.Text = msg
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
