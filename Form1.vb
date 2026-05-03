Imports System.Linq

Public Class Form1
    Private Const BASE_SYSTEM As Integer = 20
    Private Const MAX_DECIMAL_DEPTH As Integer = 5
    Private Const MIN_FRACTIONAL_PRECISION As Double = 0.0000001
    Private Const DIGIT_SIZE_MAX As Integer = 48    ' pixels when expression is short
    Private Const DIGIT_SIZE_MIN As Integer = 18    ' pixels when expression is long
    Private Const FONT_SIZE_MAX As Single = 20.0    ' operator label at full size
    Private Const FONT_SIZE_MIN As Single = 9.0     ' operator label when compressed
    Private KaktovikImages As New Dictionary(Of Integer, Image)
    Private expressionTokens As New List(Of String)
    Private currentDigits As New List(Of Integer)   ' base-20 digits 0-19
    Private hasDecimal As Boolean = False
    Private decimalDepth As Integer = 0             ' fractional digits entered so far
    Private isNewInput As Boolean = True            ' True = next digit key starts fresh
    Private isResultFinal As Boolean = False
    Private Shared ReadOnly IC As New System.Globalization.CultureInfo("en-US")
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        For i As Integer = 0 To BASE_SYSTEM - 1
            Dim img As Image = DirectCast(
                    My.Resources.Resources.ResourceManager.GetObject("Kaktovik_digit_" & i), Image)
            If img IsNot Nothing Then
                KaktovikImages.Add(i, img)
                Dim btn As Button =
                        Me.Controls.Find("Button" & i, True).Cast(Of Button).FirstOrDefault()
                If btn IsNot Nothing Then
                    btn.Image = img
                    btn.ImageAlign = ContentAlignment.MiddleCenter
                    btn.Text = ""
                End If
            End If
        Next
        FullReset()
    End Sub
    Private Sub KaktovikButton_Click(sender As Object, e As EventArgs) Handles _
            Button0.Click, Button1.Click, Button2.Click, Button3.Click, Button4.Click,
            Button5.Click, Button6.Click, Button7.Click, Button8.Click, Button9.Click,
            Button10.Click, Button11.Click, Button12.Click, Button13.Click, Button14.Click,
            Button15.Click, Button16.Click, Button17.Click, Button18.Click, Button19.Click

        Dim numValue As Integer = Integer.Parse(DirectCast(sender, Button).Name.Substring(6))

        ExitFinalResultState()

        If isNewInput Then
            currentDigits.Clear()
            hasDecimal = False
            decimalDepth = 0
            isNewInput = False
        End If

        If hasDecimal Then
            decimalDepth += 1
            If decimalDepth <= MAX_DECIMAL_DEPTH Then
                currentDigits.Add(numValue)
            End If
        Else
            If currentDigits.Count = 1 AndAlso currentDigits(0) = 0 AndAlso numValue <> 0 Then
                currentDigits.Clear()
            End If

            If currentDigits.Count >= 15 Then
                MessageBox.Show("Maximum digit limit reached.", "Limit Exceeded",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            currentDigits.Add(numValue)
        End If

        RefreshPanels()
    End Sub

    Private Sub ButtonDot_Click(sender As Object, e As EventArgs) Handles ButtonDot.Click
        If hasDecimal Then Return

        ExitFinalResultState()

        If isNewInput Then
            currentDigits.Clear()
            currentDigits.Add(0)   ' start as "0."
            isNewInput = False
        ElseIf currentDigits.Count = 0 Then
            currentDigits.Add(0)
        End If

        hasDecimal = True
        RefreshPanels()
    End Sub

    Private Sub MathOperation_Click(sender As Object, e As EventArgs) Handles _
            ButtonAdd.Click, ButtonMinus.Click, ButtonMultiply.Click, ButtonDivide.Click,
            ButtonModulo.Click, ButtonPowerup.Click

        ExitFinalResultState()
        CommitCurrentInput()

        Dim sym As String = GetOperatorSymbol(DirectCast(sender, Button).Name.Substring(6))
        If expressionTokens.Count > 0 AndAlso IsOperatorToken(expressionTokens.Last()) Then
            expressionTokens(expressionTokens.Count - 1) = sym
        Else
            expressionTokens.Add(sym)
        End If

        isNewInput = True
        hasDecimal = False
        decimalDepth = 0

        RefreshPanels()
    End Sub

    Private Sub ButtonBracket_Click(sender As Object, e As EventArgs) Handles ButtonBracket.Click
        ExitFinalResultState()
        CommitCurrentInput()

        Dim lastTok As String = If(expressionTokens.Count > 0, expressionTokens.Last(), "")
        Dim unclosed As Integer = expressionTokens.AsEnumerable().Count(Function(t) t = "(") _
                        - expressionTokens.AsEnumerable().Count(Function(t) t = ")")

        If lastTok = "" OrElse IsOperatorToken(lastTok) OrElse lastTok = "(" Then
            expressionTokens.Add("(")
        ElseIf unclosed > 0 Then
            expressionTokens.Add(")")
        Else
            expressionTokens.Add("(")
        End If

        isNewInput = True
        RefreshPanels()
    End Sub

    Private Sub ButtonEqual_Click(sender As Object, e As EventArgs) Handles ButtonEqual.Click
        CommitCurrentInput()
        If expressionTokens.Count = 0 Then Return

        Try
            Dim result As Double = EvaluateExpression(expressionTokens)

            If Double.IsInfinity(result) OrElse Double.IsNaN(result) Then
                Throw New OverflowException("Result exceeds calculable limits.")
            End If

            SeedDigitsFromDouble(result)
            expressionTokens.Clear()
            isNewInput = True
            isResultFinal = True

            RefreshPanels()

        Catch ex As Exception
            MessageBox.Show(ex.Message, "Mathematical Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
            FullReset()
        End Try
    End Sub

    Private Sub ButtonClear_Click(sender As Object, e As EventArgs) Handles ButtonClear.Click
        FullReset()
    End Sub

    Private Sub ButtonDelete_Click(sender As Object, e As EventArgs) Handles ButtonDelete.Click
        ExitFinalResultState()

        If isNewInput Then
            If expressionTokens.Count = 0 Then
                FullReset()
                Return
            End If

            Dim last As String = expressionTokens.Last()
            expressionTokens.RemoveAt(expressionTokens.Count - 1)

            If IsNumberToken(last) Then
                SeedDigitsFromDouble(Double.Parse(last, IC))
                isNewInput = False
            End If
        Else
            If hasDecimal AndAlso decimalDepth = 0 Then
                hasDecimal = False
            ElseIf currentDigits.Count > 0 Then
                currentDigits.RemoveAt(currentDigits.Count - 1)
                If hasDecimal Then decimalDepth = Math.Max(0, decimalDepth - 1)
            End If

            If currentDigits.Count = 0 Then currentDigits.Add(0)
        End If

        RefreshPanels()
    End Sub

    Private Sub ButtonAbsolute_Click(sender As Object, e As EventArgs) Handles ButtonAbsolute.Click
        ExitFinalResultState()
        SeedDigitsFromDouble(Math.Abs(CurrentInputToDouble()))
        isNewInput = True
        RefreshPanels()
    End Sub

    Private Sub ButtonFactorial_Click(sender As Object, e As EventArgs) Handles ButtonFactorial.Click
        Dim val As Double = CurrentInputToDouble()

        If val < 0 OrElse val <> Math.Truncate(val) Then
            MessageBox.Show("Factorial is only defined for non-negative integers.",
                            "Mathematical Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If val > 170 Then
            MessageBox.Show("Value too large for factorial computation.",
                            "Overflow Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim fact As Double = 1
        For i As Integer = 2 To CInt(val)
            fact *= i
        Next

        ExitFinalResultState()
        SeedDigitsFromDouble(fact)
        isNewInput = True
        RefreshPanels()
    End Sub
    Private Function ShuntingYard(tokens As List(Of String)) As Queue(Of String)
        Dim output As New Queue(Of String)
        Dim opStack As New Stack(Of String)

        For Each token As String In tokens
            If IsNumberToken(token) Then
                output.Enqueue(token)

            ElseIf token = "(" Then
                opStack.Push(token)

            ElseIf token = ")" Then
                While opStack.Count > 0 AndAlso opStack.Peek() <> "("
                    output.Enqueue(opStack.Pop())
                End While
                If opStack.Count = 0 Then
                    Throw New Exception("Mismatched parentheses in expression.")
                End If
                opStack.Pop()

            ElseIf IsOperatorToken(token) Then
                While opStack.Count > 0 AndAlso IsOperatorToken(opStack.Peek()) AndAlso
                          ((IsLeftAssociative(token) AndAlso
                            Precedence(token) <= Precedence(opStack.Peek())) OrElse
                           (Not IsLeftAssociative(token) AndAlso
                            Precedence(token) < Precedence(opStack.Peek())))
                    output.Enqueue(opStack.Pop())
                End While
                opStack.Push(token)
            End If
        Next

        While opStack.Count > 0
            Dim top As String = opStack.Pop()
            If top = "(" OrElse top = ")" Then
                Throw New Exception("Mismatched parentheses in expression.")
            End If
            output.Enqueue(top)
        End While

        Return output
    End Function

    Private Function EvaluateRPN(rpn As Queue(Of String)) As Double
        Dim stack As New Stack(Of Double)

        For Each token As String In rpn
            If IsNumberToken(token) Then
                stack.Push(Double.Parse(token, IC))

            ElseIf IsOperatorToken(token) Then
                If stack.Count < 2 Then
                    Throw New Exception("Invalid expression structure.")
                End If
                Dim b As Double = stack.Pop()
                Dim a As Double = stack.Pop()

                Select Case token
                    Case "+" : stack.Push(a + b)
                    Case "-" : stack.Push(a - b)
                    Case "×" : stack.Push(a * b)
                    Case "÷"
                        If b = 0 Then Throw New DivideByZeroException("Cannot divide by zero.")
                        stack.Push(a / b)
                    Case "%"
                        If b = 0 Then Throw New DivideByZeroException("Cannot modulo by zero.")
                        stack.Push(a Mod b)
                    Case "^" : stack.Push(Math.Pow(a, b))
                End Select
            End If
        Next

        If stack.Count <> 1 Then
            Throw New Exception("Invalid expression: mismatched operands.")
        End If
        Return stack.Pop()
    End Function

    Private Function EvaluateExpression(tokens As List(Of String)) As Double
        Return EvaluateRPN(ShuntingYard(tokens))
    End Function

    Private Sub RefreshPanels()
        RefreshOperationPanel()
        RefreshResultPanel()
    End Sub

    Private Sub RefreshResultPanel()
        ClearAndDisposeControls(pnlResult)

        Dim liveResult As Double = ComputeLiveResult()
        Dim glyphColor As Color = If(isResultFinal, Color.FromArgb(0, 150, 0), SystemColors.ControlText)

        AppendNumberToPanel(liveResult, pnlResult,
                            forceDot:=False,
                            digitSize:=DIGIT_SIZE_MAX,
                            glyphColor:=glyphColor)

        pnlResult.BackColor = If(isResultFinal,
                                 Color.FromArgb(225, 255, 225),
                                 SystemColors.Control)
    End Sub

    Private Sub RefreshOperationPanel()
        ClearAndDisposeControls(pnlOperation)

        Dim displayTokens As New List(Of String)(expressionTokens)
        If Not isNewInput Then
            If displayTokens.Count = 0 OrElse Not IsNumberToken(displayTokens.Last()) Then
                displayTokens.Add(CurrentInputToDouble().ToString("R", IC))
            End If
        End If

        Dim totalSlots As Integer = CountDisplaySlots(displayTokens)
        Dim digitSize As Integer = ComputeDigitSize(pnlOperation.ClientSize.Width, totalSlots)
        Dim fontSize As Single = ComputeOperatorFontSize(digitSize)

        For i As Integer = 0 To displayTokens.Count - 1
            Dim token As String = displayTokens(i)
            Dim isActiveInput As Boolean = (Not isNewInput AndAlso i = displayTokens.Count - 1)

            If IsNumberToken(token) Then
                Dim showTrailingDot As Boolean = isActiveInput AndAlso hasDecimal AndAlso decimalDepth = 0
                AppendNumberToPanel(Double.Parse(token, IC), pnlOperation,
                                    forceDot:=showTrailingDot,
                                    digitSize:=digitSize)

            ElseIf IsOperatorToken(token) Then
                pnlOperation.Controls.Add(MakeLabel(token, fontSize, Color.RoyalBlue))

            ElseIf token = "(" OrElse token = ")" Then
                pnlOperation.Controls.Add(MakeLabel(token, fontSize, Color.DarkOrange))
            End If
        Next
    End Sub

    Private Function ComputeLiveResult() As Double
        Dim tempTokens As New List(Of String)(expressionTokens)

        If Not isNewInput Then
            If tempTokens.Count = 0 OrElse Not IsNumberToken(tempTokens.Last()) Then
                tempTokens.Add(CurrentInputToDouble().ToString("R", IC))
            End If
        End If

        If tempTokens.Count = 0 Then Return CurrentInputToDouble()

        Try
            Dim evalTokens As List(Of String) = StripTrailingIncomplete(tempTokens)
            If evalTokens.Count = 0 Then Return CurrentInputToDouble()
            Return EvaluateExpression(evalTokens)
        Catch
            Return CurrentInputToDouble()
        End Try
    End Function

    Private Function StripTrailingIncomplete(tokens As List(Of String)) As List(Of String)
        Dim result As New List(Of String)(tokens)
        While result.Count > 0 AndAlso
              (IsOperatorToken(result.Last()) OrElse result.Last() = "(")
            result.RemoveAt(result.Count - 1)
        End While
        Return result
    End Function

    Private Function CountDisplaySlots(tokens As List(Of String)) As Integer
        Dim slots As Integer = 0
        For Each token As String In tokens
            If IsNumberToken(token) Then
                slots += CountBase20Digits(Double.Parse(token, IC))
            ElseIf IsOperatorToken(token) OrElse token = "(" OrElse token = ")" Then
                slots += 1
            End If
        Next
        Return Math.Max(1, slots)
    End Function

    Private Function CountBase20Digits(value As Double) As Integer
        Dim absVal As Double = Math.Abs(value)
        Dim intPart As Long = CLng(Math.Truncate(absVal))
        Dim fracPart As Double = absVal - intPart

        Dim intDigits As Integer = 0
        If intPart = 0 Then
            intDigits = 1
        Else
            Dim temp As Long = intPart
            While temp > 0
                intDigits += 1
                temp = temp \ BASE_SYSTEM
            End While
        End If

        Dim fracDigits As Integer = 0
        Dim depth As Integer = 0
        While fracPart > MIN_FRACTIONAL_PRECISION AndAlso depth < MAX_DECIMAL_DEPTH
            fracPart *= BASE_SYSTEM
            fracPart -= Math.Truncate(fracPart)
            fracDigits += 1
            depth += 1
        End While

        Return intDigits + fracDigits
    End Function

    Private Function ComputeDigitSize(panelWidth As Integer, slotCount As Integer) As Integer
        Dim sizePerSlot As Integer = panelWidth \ Math.Max(1, slotCount)
        Return Math.Min(DIGIT_SIZE_MAX, Math.Max(DIGIT_SIZE_MIN, sizePerSlot))
    End Function

    Private Function ComputeOperatorFontSize(digitSize As Integer) As Single
        Dim ratio As Single = (digitSize - DIGIT_SIZE_MIN) / CSng(DIGIT_SIZE_MAX - DIGIT_SIZE_MIN)
        Return FONT_SIZE_MIN + ratio * (FONT_SIZE_MAX - FONT_SIZE_MIN)
    End Function

    Private Sub AppendNumberToPanel(value As Double, target As Panel,
                                    Optional forceDot As Boolean = False,
                                    Optional digitSize As Integer = DIGIT_SIZE_MAX,
                                    Optional glyphColor As Color = Nothing)

        Dim labelColor As Color = If(glyphColor.IsEmpty, SystemColors.ControlText, glyphColor)
        Dim labelFontSize As Single = CSng(digitSize * 0.55)

        If value < 0 Then
            target.Controls.Add(MakeLabel("-", labelFontSize, labelColor))
        End If

        Dim absVal As Double = Math.Abs(value)
        Dim intPart As Long = CLng(Math.Truncate(absVal))
        Dim fracPart As Double = absVal - intPart

        If intPart = 0 Then
            AddGlyphToPanel(0, target, digitSize)
        Else
            Dim digits As New List(Of Integer)
            Dim temp As Long = intPart
            While temp > 0
                digits.Add(CInt(temp Mod BASE_SYSTEM))
                temp = temp \ BASE_SYSTEM
            End While
            digits.Reverse()
            For Each d In digits
                AddGlyphToPanel(d, target, digitSize)
            Next
        End If

        If forceDot OrElse fracPart > MIN_FRACTIONAL_PRECISION Then
            target.Controls.Add(MakeLabel(".", labelFontSize, labelColor))
            Dim count As Integer = 0
            While fracPart > MIN_FRACTIONAL_PRECISION AndAlso count < MAX_DECIMAL_DEPTH
                fracPart *= BASE_SYSTEM
                Dim fd As Integer = CInt(Math.Truncate(fracPart))
                AddGlyphToPanel(fd, target, digitSize)
                fracPart -= fd
                count += 1
            End While
        End If
    End Sub

    Private Sub AddGlyphToPanel(digitValue As Integer, target As Panel, digitSize As Integer)
        Dim pb As New PictureBox With {
            .Size = New Size(digitSize, digitSize),
            .SizeMode = PictureBoxSizeMode.Zoom,
            .Margin = New Padding(1)
        }
        If KaktovikImages.ContainsKey(digitValue) Then pb.Image = KaktovikImages(digitValue)
        target.Controls.Add(pb)
    End Sub

    Private Function MakeLabel(text As String, fontSize As Single, color As Color) As Label
        Return New Label With {
            .Text = text,
            .Font = New Font("Arial", Math.Max(8.0F, fontSize), FontStyle.Bold),
            .ForeColor = color,
            .AutoSize = True,
            .Padding = New Padding(0, 4, 0, 0)
        }
    End Function

    Private Sub ClearAndDisposeControls(panel As Panel)
        For Each ctrl As Control In panel.Controls
            ctrl.Dispose()
        Next
        panel.Controls.Clear()
    End Sub

    Private Function CurrentInputToDouble() As Double
        If currentDigits.Count = 0 Then Return 0.0

        Dim intCount As Integer =
                If(hasDecimal, currentDigits.Count - decimalDepth, currentDigits.Count)
        intCount = Math.Max(0, intCount)

        Dim intVal As Long = 0
        For i As Integer = 0 To intCount - 1
            intVal = intVal * BASE_SYSTEM + currentDigits(i)
        Next

        Dim fracVal As Double = 0.0
        For i As Integer = intCount To currentDigits.Count - 1
            fracVal += currentDigits(i) / Math.Pow(BASE_SYSTEM, i - intCount + 1)
        Next

        Return intVal + fracVal
    End Function

    Private Sub SeedDigitsFromDouble(value As Double)
        currentDigits.Clear()
        hasDecimal = False
        decimalDepth = 0

        Dim absVal As Double = Math.Abs(value)
        Dim intPart As Long = CLng(Math.Truncate(absVal))
        Dim fracPart As Double = absVal - intPart

        If intPart = 0 Then
            currentDigits.Add(0)
        Else
            Dim temp As Long = intPart
            Dim intDigits As New List(Of Integer)
            While temp > 0
                intDigits.Add(CInt(temp Mod BASE_SYSTEM))
                temp = temp \ BASE_SYSTEM
            End While
            intDigits.Reverse()
            currentDigits.AddRange(intDigits)
        End If

        If fracPart > MIN_FRACTIONAL_PRECISION Then
            hasDecimal = True
            Dim count As Integer = 0
            While fracPart > MIN_FRACTIONAL_PRECISION AndAlso count < MAX_DECIMAL_DEPTH
                fracPart *= BASE_SYSTEM
                Dim fd As Integer = CInt(Math.Truncate(fracPart))
                currentDigits.Add(fd)
                fracPart -= fd
                count += 1
                decimalDepth += 1
            End While
        End If
    End Sub

    Private Sub CommitCurrentInput()
        If isNewInput Then Return

        Dim lastTok As String = If(expressionTokens.Count > 0, expressionTokens.Last(), "")
        If IsNumberToken(lastTok) Then Return

        expressionTokens.Add(CurrentInputToDouble().ToString("R", IC))
        isNewInput = True
    End Sub

    Private Function IsOperatorToken(token As String) As Boolean
        Return {"+", "-", "×", "÷", "%", "^"}.Contains(token)
    End Function

    Private Function IsNumberToken(token As String) As Boolean
        Dim dummy As Double
        Return Double.TryParse(token, System.Globalization.NumberStyles.Any, IC, dummy)
    End Function
    Private Function Precedence(op As String) As Integer
        Select Case op
            Case "+", "-" : Return 1
            Case "×", "÷", "%" : Return 2
            Case "^" : Return 3
            Case Else : Return 0
        End Select
    End Function
    Private Function IsLeftAssociative(op As String) As Boolean
        Return op <> "^"
    End Function

    Private Function GetOperatorSymbol(opName As String) As String
        Select Case opName
            Case "Add" : Return "+"
            Case "Minus" : Return "-"
            Case "Multiply" : Return "×"
            Case "Divide" : Return "÷"
            Case "Modulo" : Return "%"
            Case "Powerup" : Return "^"
            Case Else : Return ""
        End Select
    End Function

    Private Sub ExitFinalResultState()
        If Not isResultFinal Then Return
        isResultFinal = False
        pnlResult.BackColor = SystemColors.Control
    End Sub

    Private Sub FullReset()
        currentDigits.Clear()
        currentDigits.Add(0)
        hasDecimal = False
        decimalDepth = 0
        isNewInput = True
        isResultFinal = False
        expressionTokens.Clear()

        ClearAndDisposeControls(pnlOperation)
        ClearAndDisposeControls(pnlResult)
        pnlResult.BackColor = SystemColors.Control

        AppendNumberToPanel(0, pnlResult, digitSize:=DIGIT_SIZE_MAX)
    End Sub

End Class