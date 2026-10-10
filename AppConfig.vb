Imports System.IO
Imports System.Text.Json

''' <summary>One phrase to look for, and which actions to perform when it appears.</summary>
Public Class PhraseRule
    Public Property Phrase As String = ""
    Public Property Flash As Boolean = True
    Public Property Sound As Boolean = True
    Public Property Log As Boolean = True
    Public Property Run As Boolean = False
    Public Property FlashColor As String = "#FF0000"
    Public Property SoundFile As String = ""
    Public Property CommandPath As String = ""
    Public Property CommandArgs As String = ""
End Class

''' <summary>All persisted settings. Stored as config.json next to the executable.</summary>
Public Class AppConfig

    Public Property HasRegion As Boolean = False
    Public Property RegionX As Integer
    Public Property RegionY As Integer
    Public Property RegionWidth As Integer
    Public Property RegionHeight As Integer

    Public Property PollIntervalMs As Integer = 1000
    Public Property RearmAfterAbsentPolls As Integer = 10
    Public Property UpscaleFactor As Integer = 2

    Public Property FlashDurationMs As Integer = 400

    Public Property LogFile As String = ""

    Public Property Phrases As List(Of PhraseRule) = New List(Of PhraseRule)()

    Public Shared ReadOnly Property ConfigPath As String
        Get
            Return Path.Combine(AppContext.BaseDirectory, "config.json")
        End Get
    End Property

    <System.Text.Json.Serialization.JsonIgnore>
    Public Property Region As Rectangle
        Get
            Return New Rectangle(RegionX, RegionY, RegionWidth, RegionHeight)
        End Get
        Set(value As Rectangle)
            HasRegion = True
            RegionX = value.X
            RegionY = value.Y
            RegionWidth = value.Width
            RegionHeight = value.Height
        End Set
    End Property

    Public Shared Function Load() As AppConfig
        Try
            If File.Exists(ConfigPath) Then
                Dim cfg = JsonSerializer.Deserialize(Of AppConfig)(File.ReadAllText(ConfigPath))
                If cfg IsNot Nothing Then
                    cfg.Sanitize()
                    Return cfg
                End If
            End If
        Catch
            ' Corrupt config: fall back to defaults rather than refusing to start.
        End Try
        Return New AppConfig()
    End Function

    Public Sub Save()
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Me, opts))
    End Sub

    Private Sub Sanitize()
        PollIntervalMs = Math.Clamp(PollIntervalMs, 100, 60000)
        RearmAfterAbsentPolls = Math.Clamp(RearmAfterAbsentPolls, 1, 1000)
        UpscaleFactor = Math.Clamp(UpscaleFactor, 1, 4)
        FlashDurationMs = Math.Clamp(FlashDurationMs, 50, 5000)
        If RegionWidth < 1 OrElse RegionHeight < 1 Then HasRegion = False
        If Phrases Is Nothing Then Phrases = New List(Of PhraseRule)()
        If LogFile Is Nothing Then LogFile = ""
        Phrases.RemoveAll(Function(p) p Is Nothing)
        For Each p In Phrases
            If p.Phrase Is Nothing Then p.Phrase = ""
            If p.FlashColor Is Nothing Then p.FlashColor = "#FF0000"
            If p.SoundFile Is Nothing Then p.SoundFile = ""
            If p.CommandPath Is Nothing Then p.CommandPath = ""
            If p.CommandArgs Is Nothing Then p.CommandArgs = ""
        Next
    End Sub

End Class
