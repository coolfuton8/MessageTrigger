Imports System.IO
Imports System.Media

''' <summary>Performs the configured actions for a triggered phrase.</summary>
Public Class ActionRunner

    ' Held so playback isn't garbage-collected mid-sound.
    Private Shared _wavPlayer As SoundPlayer
    Private Shared _mediaPlayer As Windows.Media.Playback.MediaPlayer

    Public Shared Sub Execute(cfg As AppConfig, rule As PhraseRule, context As String, reportError As Action(Of String))
        If rule.Flash Then
            Attempt("Flash", reportError, Sub() FlashOverlay.Flash(ParseColor(rule.FlashColor), cfg.FlashDurationMs))
        End If
        If rule.Sound Then
            Attempt("Sound", reportError, Sub() PlaySound(rule.SoundFile))
        End If
        If rule.Log Then
            Attempt("Log", reportError, Sub() WriteLog(cfg.LogFile, rule.Phrase, context))
        End If
        If rule.Run Then
            Attempt("Run", reportError, Sub() RunCommand(rule.CommandPath, rule.CommandArgs, rule.Phrase))
        End If
    End Sub

    Public Shared Function ParseColor(s As String) As Color
        Try
            Return ColorTranslator.FromHtml(s)
        Catch
            Return Color.Red
        End Try
    End Function

    Public Shared Sub PlaySound(path As String)
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then
            SystemSounds.Exclamation.Play()
        ElseIf String.Equals(System.IO.Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase) Then
            _wavPlayer = New SoundPlayer(path)
            _wavPlayer.Play()
        Else
            If _mediaPlayer Is Nothing Then _mediaPlayer = New Windows.Media.Playback.MediaPlayer()
            _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(New Uri(path))
            _mediaPlayer.Play()
        End If
    End Sub

    Public Shared Sub WriteLog(path As String, phrase As String, context As String)
        If String.IsNullOrWhiteSpace(path) Then Throw New InvalidOperationException("No log file configured.")
        Dim dir As String = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))
        If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
        Dim line As String = $"{Now:yyyy-MM-dd HH:mm:ss} | {phrase} | {context}"
        File.AppendAllText(path, line & Environment.NewLine)
    End Sub

    ''' <summary>
    ''' Launches the configured program/script. {phrase} (the configured phrase, never raw OCR text)
    ''' and {time} are substituted into the arguments.
    ''' </summary>
    Public Shared Sub RunCommand(commandPath As String, args As String, phrase As String)
        If String.IsNullOrWhiteSpace(commandPath) Then Throw New InvalidOperationException("No program configured.")
        Dim psi As New ProcessStartInfo(commandPath) With {
            .UseShellExecute = True,
            .Arguments = (If(args, "")).Replace("{phrase}", phrase).Replace("{time}", Now.ToString("yyyy-MM-dd_HH-mm-ss"))
        }
        If File.Exists(commandPath) Then
            psi.WorkingDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(commandPath))
        End If
        Using Process.Start(psi)
        End Using
    End Sub

    Private Shared Sub Attempt(name As String, reportError As Action(Of String), act As Action)
        Try
            act()
        Catch ex As Exception
            reportError?.Invoke($"{name} action failed: {ex.Message}")
        End Try
    End Sub

End Class
