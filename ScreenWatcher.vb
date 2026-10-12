Imports System.Threading

''' <summary>
''' Polls the capture region, OCRs it, and raises <see cref="Triggered"/> per phrase.
''' A phrase fires once, then must be absent for N consecutive polls before it can fire again.
''' All events are raised on the thread that called <see cref="Start"/> (the UI thread).
''' The config object passed to Start is read on every poll, so changes to it apply live.
''' </summary>
Public Class ScreenWatcher

    Private Class PhraseState
        Public Armed As Boolean = True
        Public AbsentPolls As Integer = 0
    End Class

    Public Event TextRead(text As String)
    Public Event Triggered(rule As PhraseRule, context As String)
    Public Event ErrorOccurred(message As String)

    ''' <summary>Raised after every poll attempt (successful or not): the "heartbeat".</summary>
    Public Event PollCompleted(pollNumber As Integer)

    ''' <summary>failure is Nothing for a normal stop, otherwise why watching had to stop.</summary>
    Public Event Stopped(failure As String)

    Private _cts As CancellationTokenSource
    Private _delayCts As CancellationTokenSource
    Private _pollCount As Integer
    Private ReadOnly _state As New Dictionary(Of String, PhraseState)

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _cts IsNot Nothing
        End Get
    End Property

    ''' <summary>While true the loop keeps running but skips capture (e.g. during region selection).</summary>
    Public Property Paused As Boolean

    Public ReadOnly Property PollCount As Integer
        Get
            Return _pollCount
        End Get
    End Property

    Public Sub Start(cfg As AppConfig)
        If IsRunning Then Return
        _state.Clear()
        _pollCount = 0
        Paused = False
        _cts = New CancellationTokenSource()
        Dim ignored As Task = RunAsync(cfg, _cts.Token)
    End Sub

    Public Sub [Stop]()
        If _cts IsNot Nothing Then _cts.Cancel()
    End Sub

    ''' <summary>Cuts the current wait short so the next poll happens now (used when the interval changes).</summary>
    Public Sub Nudge()
        Try
            If _delayCts IsNot Nothing Then _delayCts.Cancel()
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Async Function RunAsync(cfg As AppConfig, ct As CancellationToken) As Task
        Dim ocr As ScreenOcr
        Try
            ocr = New ScreenOcr()
        Catch ex As Exception
            RaiseEvent ErrorOccurred(ex.Message)
            Finish(ex.Message)
            Return
        End Try

        Do While Not ct.IsCancellationRequested
            If Not Paused Then
                Try
                    Dim text As String
                    Using bmp As Bitmap = ScreenOcr.Capture(cfg.Region)
                        text = Await ocr.RecognizeAsync(bmp, cfg.UpscaleFactor)
                    End Using
                    If ct.IsCancellationRequested Then Exit Do
                    RaiseEvent TextRead(text)
                    Evaluate(cfg, text)
                Catch ex As Exception
                    RaiseEvent ErrorOccurred(ex.Message)
                End Try
                _pollCount += 1
                RaiseEvent PollCompleted(_pollCount)
            End If

            Using linked As CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct)
                _delayCts = linked
                Try
                    Await Task.Delay(If(Paused, 200, Math.Max(100, cfg.PollIntervalMs)), linked.Token)
                Catch ex As TaskCanceledException
                    If ct.IsCancellationRequested Then Exit Do
                    ' otherwise nudged: fall through and poll right away
                Finally
                    _delayCts = Nothing
                End Try
            End Using
        Loop

        Finish(Nothing)
    End Function

    Private Sub Finish(failure As String)
        _cts?.Dispose()
        _cts = Nothing
        RaiseEvent Stopped(failure)
    End Sub

    Private Sub Evaluate(cfg As AppConfig, text As String)
        Dim normText As String = Normalize(text)
        Dim lines As String() = text.Split({vbLf, vbCr}, StringSplitOptions.RemoveEmptyEntries)

        For Each rule In cfg.Phrases
            Dim key As String = Normalize(rule.Phrase)
            If key.Length = 0 Then Continue For

            Dim st As PhraseState = Nothing
            If Not _state.TryGetValue(key, st) Then
                st = New PhraseState()
                _state(key) = st
            End If

            If normText.Contains(key) Then
                st.AbsentPolls = 0
                If st.Armed Then
                    st.Armed = False
                    Dim context As String = lines.FirstOrDefault(Function(l) Normalize(l).Contains(key))
                    If context Is Nothing Then context = text.Replace(vbLf, " ").Trim()
                    RaiseEvent Triggered(rule, context.Trim())
                End If
            Else
                st.AbsentPolls += 1
                If st.AbsentPolls >= cfg.RearmAfterAbsentPolls Then st.Armed = True
            End If
        Next
    End Sub

    ''' <summary>Case/whitespace/punctuation-insensitive form, so OCR spacing noise doesn't break matches.</summary>
    Private Shared Function Normalize(s As String) As String
        If String.IsNullOrEmpty(s) Then Return ""
        Dim sb As New System.Text.StringBuilder(s.Length)
        For Each c As Char In s
            If Char.IsLetterOrDigit(c) Then sb.Append(Char.ToLowerInvariant(c))
        Next
        Return sb.ToString()
    End Function

End Class
