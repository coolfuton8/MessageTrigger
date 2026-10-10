Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports Windows.Graphics.Imaging
Imports Windows.Media.Ocr

''' <summary>Captures a screen rectangle and runs the built-in Windows OCR engine on it.</summary>
Public Class ScreenOcr

    Private ReadOnly _engine As OcrEngine

    Public Sub New()
        _engine = OcrEngine.TryCreateFromUserProfileLanguages()
        If _engine Is Nothing Then
            Throw New InvalidOperationException(
                "Windows OCR is not available: no OCR language pack is installed for your user profile languages. " &
                "Add a language under Settings > Time & language > Language & region.")
        End If
    End Sub

    Public Shared Function Capture(area As Rectangle) As Bitmap
        Dim bmp As New Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb)
        Using g As Graphics = Graphics.FromImage(bmp)
            g.CopyFromScreen(area.Location, Point.Empty, area.Size)
        End Using
        Return bmp
    End Function

    Public Async Function RecognizeAsync(source As Bitmap, upscale As Integer) As Task(Of String)
        Dim maxDim As Integer = CInt(OcrEngine.MaxImageDimension)
        Dim factor As Integer = Math.Max(1, upscale)
        While factor > 1 AndAlso (source.Width * factor > maxDim OrElse source.Height * factor > maxDim)
            factor -= 1
        End While

        Dim work As Bitmap = source
        If factor > 1 Then
            work = New Bitmap(source.Width * factor, source.Height * factor, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(work)
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.PixelOffsetMode = PixelOffsetMode.HighQuality
                g.DrawImage(source, 0, 0, work.Width, work.Height)
            End Using
        End If

        Try
            If work.Width > maxDim OrElse work.Height > maxDim Then
                Throw New InvalidOperationException($"Capture area is larger than the OCR limit of {maxDim}px.")
            End If

            Using ms As New MemoryStream()
                work.Save(ms, ImageFormat.Png)
                ms.Position = 0
                Dim decoder As BitmapDecoder = Await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream()).AsTask()
                Using sb As SoftwareBitmap = Await decoder.GetSoftwareBitmapAsync().AsTask()
                    Dim result As OcrResult = Await _engine.RecognizeAsync(sb).AsTask()
                    Return String.Join(vbLf, result.Lines.Select(Function(l) l.Text))
                End Using
            End Using
        Finally
            If Not Object.ReferenceEquals(work, source) Then work.Dispose()
        End Try
    End Function

End Class
