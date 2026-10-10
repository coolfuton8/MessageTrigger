''' <summary>Click-through, non-activating, topmost window base (so the game underneath stays usable).</summary>
Public MustInherit Class ClickThroughForm
    Inherits Form

    Private Const WS_EX_TRANSPARENT As Integer = &H20
    Private Const WS_EX_TOOLWINDOW As Integer = &H80
    Private Const WS_EX_NOACTIVATE As Integer = &H8000000
    Private Const WS_EX_LAYERED As Integer = &H80000

    Protected Sub New()
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp As CreateParams = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or WS_EX_TRANSPARENT Or WS_EX_TOOLWINDOW Or WS_EX_NOACTIVATE Or WS_EX_LAYERED
            Return cp
        End Get
    End Property

End Class

''' <summary>Thin frame drawn just OUTSIDE the capture region; everything else is transparent.</summary>
Public Class FrameOverlay
    Inherits ClickThroughForm

    Public Const Thickness As Integer = 3
    Private ReadOnly _key As Color = Color.Magenta

    Public Sub New()
        BackColor = _key
        TransparencyKey = _key
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer, True)
    End Sub

    Public Sub ShowAround(region As Rectangle)
        Bounds = Rectangle.Inflate(region, Thickness, Thickness)
        If Not Visible Then Show()
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Using b As New SolidBrush(Color.Lime)
            Dim w As Integer = ClientSize.Width
            Dim h As Integer = ClientSize.Height
            e.Graphics.FillRectangle(b, 0, 0, w, Thickness)
            e.Graphics.FillRectangle(b, 0, h - Thickness, w, Thickness)
            e.Graphics.FillRectangle(b, 0, 0, Thickness, h)
            e.Graphics.FillRectangle(b, w - Thickness, 0, Thickness, h)
        End Using
    End Sub

End Class

''' <summary>Briefly tints one whole screen.</summary>
Public Class FlashOverlay
    Inherits ClickThroughForm

    ' First half of the duration: 3 strobe pulses. Second half: gradual fade-out.
    Private Const StrobeCount As Integer = 3
    Private Const PeakOpacity As Double = 0.6

    Private ReadOnly _timer As New System.Windows.Forms.Timer()
    Private ReadOnly _clock As New Stopwatch()
    Private ReadOnly _durationMs As Double

    Private Sub New(bounds As Rectangle, color As Color, durationMs As Integer)
        Me.Bounds = bounds
        BackColor = color
        Opacity = PeakOpacity
        _durationMs = Math.Max(200, durationMs)
        _timer.Interval = 16
        AddHandler _timer.Tick, AddressOf OnTick
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        Dim t As Double = _clock.ElapsedMilliseconds / _durationMs
        If t >= 1.0 Then
            _timer.Stop()
            Close()
        ElseIf t < 0.5 Then
            Dim cycle As Double = (t / 0.5 * StrobeCount) Mod 1.0
            Opacity = If(cycle < 0.5, PeakOpacity, 0.0)
        Else
            Dim f As Double = (t - 0.5) / 0.5
            Opacity = PeakOpacity * (1.0 - f) * (1.0 - f) ' eased so the tail lingers softly
        End If
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        _clock.Start()
        _timer.Start()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then _timer.Dispose()
        MyBase.Dispose(disposing)
    End Sub

    Public Shared Sub Flash(color As Color, durationMs As Integer)
        For Each scr In Screen.AllScreens
            Dim f As New FlashOverlay(scr.Bounds, color, durationMs)
            f.Show()
        Next
    End Sub

End Class

''' <summary>Full-screen dimmed overlay (one per monitor) for dragging out the capture rectangle.</summary>
Public Class SelectionOverlay
    Inherits Form

    Private ReadOnly _screenBounds As Rectangle
    Private _start As Point
    Private _current As Point
    Private _dragging As Boolean

    Public Event Done(selected As Rectangle?)

    Private Sub New(screenBounds As Rectangle)
        _screenBounds = screenBounds
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        TopMost = True
        StartPosition = FormStartPosition.Manual
        Bounds = screenBounds
        BackColor = Color.Black
        Opacity = 0.35
        Cursor = Cursors.Cross
        KeyPreview = True
        DoubleBuffered = True
    End Sub

    ''' <summary>Shows an overlay on every monitor; returns the chosen screen rectangle, or Nothing if cancelled.</summary>
    Public Shared Async Function PickAsync() As Task(Of Rectangle?)
        Dim tcs As New TaskCompletionSource(Of Rectangle?)()
        Dim forms As New List(Of SelectionOverlay)()

        For Each scr In Screen.AllScreens
            Dim f As New SelectionOverlay(scr.Bounds)
            AddHandler f.Done, Sub(r As Rectangle?)
                                   If tcs.TrySetResult(r) Then
                                       For Each o In forms
                                           o.Close()
                                       Next
                                   End If
                               End Sub
            forms.Add(f)
        Next

        For Each f In forms
            f.Show()
            f.Bounds = f._screenBounds ' re-assert after handle creation (mixed-DPI monitors)
        Next
        Dim under = forms.FirstOrDefault(Function(f) f._screenBounds.Contains(Cursor.Position))
        If under IsNot Nothing Then under.Activate()

        Return Await tcs.Task
    End Function

    Private Function CurrentRect() As Rectangle
        Return New Rectangle(Math.Min(_start.X, _current.X), Math.Min(_start.Y, _current.Y),
                             Math.Abs(_current.X - _start.X), Math.Abs(_current.Y - _start.Y))
    End Function

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            _dragging = True
            _start = e.Location
            _current = e.Location
        Else
            RaiseEvent Done(Nothing)
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If _dragging Then
            _current = e.Location
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        If Not _dragging Then Return
        _dragging = False
        Dim r As Rectangle = CurrentRect()
        If r.Width < 8 OrElse r.Height < 8 Then
            RaiseEvent Done(Nothing)
        Else
            r.Offset(_screenBounds.Location)
            RaiseEvent Done(r)
        End If
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.KeyCode = Keys.Escape Then RaiseEvent Done(Nothing)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Using f As New Font("Segoe UI", 14.0F, FontStyle.Bold)
            e.Graphics.DrawString("Drag to select the area to watch.  Esc / right-click to cancel.", f, Brushes.White, 20, 20)
        End Using
        If _dragging Then
            Dim r As Rectangle = CurrentRect()
            e.Graphics.FillRectangle(Brushes.White, r)
            Using p As New Pen(Color.Lime, 2)
                e.Graphics.DrawRectangle(p, r)
            End Using
        End If
    End Sub

End Class
