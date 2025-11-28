using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Layout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Parameter information for display.
/// </summary>
public class ParamInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsOptional { get; set; }
    public string DefaultValue { get; set; } = "";
}

/// <summary>
/// Method signature information for display.
/// </summary>
public class MethodSig
{
    public string MethodName { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public List<ParamInfo> Parameters { get; set; } = new();
}

/// <summary>
/// Container for parameter hint information.
/// </summary>
public class ParamHints
{
    public string MethodName { get; set; } = "";
    public List<MethodSig> Overloads { get; set; } = new();
    public int CurrentOverloadIndex { get; set; }
}

/// <summary>
/// Tooltip that displays method parameter hints with the current parameter highlighted.
/// </summary>
public class ParameterHintTooltip : UIComponent
{
    private ParamHints? _hintInfo = null;
    private int _currentParameterIndex = 0;

    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _borderColor;
    private Color? _methodNameColor;
    private Color? _parameterColor;
    private Color? _currentParameterColor;
    private Color? _typeColor;
    private Color? _counterColor;

    public Color BackgroundColor { get => _backgroundColor ?? ThemeManager.Current.BackgroundElevated; set => _backgroundColor = value; }
    public Color BorderColor { get => _borderColor ?? ThemeManager.Current.BorderPrimary; set => _borderColor = value; }
    public Color MethodNameColor { get => _methodNameColor ?? ThemeManager.Current.SyntaxMethod; set => _methodNameColor = value; }
    public Color ParameterColor { get => _parameterColor ?? ThemeManager.Current.TextSecondary; set => _parameterColor = value; }
    public Color CurrentParameterColor { get => _currentParameterColor ?? ThemeManager.Current.TextPrimary; set => _currentParameterColor = value; }
    public Color TypeColor { get => _typeColor ?? ThemeManager.Current.SyntaxType; set => _typeColor = value; }
    public Color CounterColor { get => _counterColor ?? ThemeManager.Current.TextDim; set => _counterColor = value; }
    public float Padding { get; set; } = 8f;
    public float BorderThickness { get; set; } = 1f;

    public ParameterHintTooltip(string id)
    {
        Id = id;
    }

    /// <summary>
    /// Sets the parameter hints to display.
    /// </summary>
    public void SetHints(ParamHints? hints, int currentParameterIndex = 0)
    {
        _hintInfo = hints;
        _currentParameterIndex = Math.Max(0, currentParameterIndex);

        // Update height immediately when hints change
        if (_hintInfo != null && _hintInfo.Overloads.Count > 0)
        {
            try
            {
                var height = GetDesiredHeight(Renderer);
                Constraint.Height = height;
            }
            catch
            {
                // Renderer might not be available yet, will be set in OnRender
            }
        }
        else
        {
            Constraint.Height = 0;
        }
    }

    /// <summary>
    /// Clears all hints.
    /// </summary>
    public void Clear()
    {
        _hintInfo = null;
        _currentParameterIndex = 0;
    }

    /// <summary>
    /// Cycles to the next method overload.
    /// </summary>
    public void NextOverload()
    {
        if (_hintInfo != null && _hintInfo.Overloads.Count > 1)
        {
            _hintInfo.CurrentOverloadIndex = (_hintInfo.CurrentOverloadIndex + 1) % _hintInfo.Overloads.Count;

            // Update height for new overload
            try
            {
                var height = GetDesiredHeight(Renderer);
                Constraint.Height = height;
            }
            catch
            {
                // Renderer might not be available yet
            }
        }
    }

    /// <summary>
    /// Cycles to the previous method overload.
    /// </summary>
    public void PreviousOverload()
    {
        if (_hintInfo != null && _hintInfo.Overloads.Count > 1)
        {
            _hintInfo.CurrentOverloadIndex--;
            if (_hintInfo.CurrentOverloadIndex < 0)
                _hintInfo.CurrentOverloadIndex = _hintInfo.Overloads.Count - 1;

            // Update height for new overload
            try
            {
                var height = GetDesiredHeight(Renderer);
                Constraint.Height = height;
            }
            catch
            {
                // Renderer might not be available yet
            }
        }
    }

    /// <summary>
    /// Gets whether the tooltip has content to display.
    /// </summary>
    public bool HasContent => _hintInfo != null && _hintInfo.Overloads.Count > 0;

    protected override void OnRender(UIContext context)
    {
        if (!HasContent || Rect.Height <= 0)
            return;

        var renderer = Renderer;

        var resolvedRect = Rect;

        var currentSignature = _hintInfo!.Overloads[_hintInfo.CurrentOverloadIndex];

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border
        renderer.DrawRectangleOutline(resolvedRect, BorderColor, (int)BorderThickness);

        var lineHeight = renderer.GetLineHeight();
        var yPos = resolvedRect.Y + Padding;

        // Draw method signature
        var xPos = resolvedRect.X + Padding;

        // Method name and opening paren
        var methodText = currentSignature.MethodName + "(";
        renderer.DrawText(methodText, new Vector2(xPos, yPos), MethodNameColor);

        // If no parameters, close on same line
        if (currentSignature.Parameters.Count == 0)
        {
            xPos += renderer.MeasureText(methodText).X;
            var closingText = ")";
            renderer.DrawText(closingText, new Vector2(xPos, yPos), MethodNameColor);
            xPos += renderer.MeasureText(closingText).X;

            // Return type
            if (!string.IsNullOrEmpty(currentSignature.ReturnType))
            {
                var returnText = " : " + currentSignature.ReturnType;
                renderer.DrawText(returnText, new Vector2(xPos, yPos), TypeColor);
            }
        }
        else
        {
            // Move to next line for parameters
            yPos += lineHeight;

            // Parameters - each on its own line for readability
            for (int i = 0; i < currentSignature.Parameters.Count; i++)
            {
                var param = currentSignature.Parameters[i];
                var isCurrentParam = i == _currentParameterIndex;
                var paramColor = isCurrentParam ? CurrentParameterColor : ParameterColor;

                xPos = resolvedRect.X + Padding + 20; // Indent parameters

                // Type
                var typeText = param.Type + " ";
                renderer.DrawText(typeText, new Vector2(xPos, yPos), TypeColor);
                xPos += renderer.MeasureText(typeText).X;

                // Parameter name
                var paramText = param.Name;
                if (param.IsOptional)
                    paramText += " = " + param.DefaultValue;

                // Highlight background for current parameter
                if (isCurrentParam)
                {
                    var paramSize = renderer.MeasureText(typeText + paramText);
                    var highlightRect = new LayoutRect(
                        resolvedRect.X + Padding + 16,
                        yPos - 2,
                        paramSize.X + 8,
                        lineHeight
                    );
                    renderer.DrawRectangle(highlightRect, ThemeManager.Current.InputSelection);
                }

                renderer.DrawText(paramText, new Vector2(xPos, yPos), paramColor);
                xPos += renderer.MeasureText(paramText).X;

                // Comma separator (except for last param)
                if (i < currentSignature.Parameters.Count - 1)
                {
                    var commaText = ",";
                    renderer.DrawText(commaText, new Vector2(xPos, yPos), ParameterColor);
                }

                yPos += lineHeight;
            }

            // Closing parenthesis
            xPos = resolvedRect.X + Padding;
            var closingText = ")";
            renderer.DrawText(closingText, new Vector2(xPos, yPos), MethodNameColor);
            xPos += renderer.MeasureText(closingText).X;

            // Return type
            if (!string.IsNullOrEmpty(currentSignature.ReturnType))
            {
                var returnText = " : " + currentSignature.ReturnType;
                renderer.DrawText(returnText, new Vector2(xPos, yPos), TypeColor);
            }
        }

        // Draw overload counter on its own line at the bottom right
        if (_hintInfo.Overloads.Count > 1)
        {
            yPos += lineHeight;
            var counterText = $"[{_hintInfo.CurrentOverloadIndex + 1}/{_hintInfo.Overloads.Count}]";
            var counterSize = renderer.MeasureText(counterText);
            var counterX = resolvedRect.Right - Padding - counterSize.X;
            renderer.DrawText(counterText, new Vector2(counterX, yPos), CounterColor);
        }
    }

    /// <summary>
    /// Calculates the desired height for the tooltip based on content.
    /// </summary>
    public float GetDesiredHeight(UIRenderer? renderer = null)
    {
        if (!HasContent)
            return 0;

        float lineHeight = 20f; // Default fallback

        if (renderer != null)
        {
            lineHeight = renderer.GetLineHeight();
        }
        else
        {
            try
            {
                if (Renderer != null)
                    lineHeight = Renderer.GetLineHeight();
            }
            catch
            {
                // No context available, use default
            }
        }

        var currentSignature = _hintInfo!.Overloads[_hintInfo.CurrentOverloadIndex];

        // Calculate height based on multi-line layout
        float height = Padding * 2; // Top and bottom padding

        // Method name line
        height += lineHeight;

        // Parameter lines (each param on its own line)
        if (currentSignature.Parameters.Count > 0)
        {
            height += lineHeight * currentSignature.Parameters.Count;

            // Closing paren and return type line
            height += lineHeight;
        }

        // If multiple overloads, add space for counter line at bottom
        if (_hintInfo.Overloads.Count > 1)
        {
            height += lineHeight;
        }

        return height;
    }

    /// <summary>
    /// Calculates the desired width for the tooltip based on content.
    /// </summary>
    public float GetDesiredWidth(UIRenderer? renderer = null)
    {
        if (!HasContent)
            return 0;

        UIRenderer? r = renderer ?? Renderer;
        if (r == null)
            return 300f; // Default fallback

        var currentSignature = _hintInfo!.Overloads[_hintInfo.CurrentOverloadIndex];
        float maxWidth = 0;

        // Method name line width
        var methodNameText = currentSignature.MethodName + "(" + (currentSignature.Parameters.Count == 0 ? ")" : "");
        var methodNameWidth = r.MeasureText(methodNameText).X;
        maxWidth = Math.Max(maxWidth, methodNameWidth);

        // Check each parameter line width (indented)
        foreach (var param in currentSignature.Parameters)
        {
            var paramText = param.Type + " " + param.Name;
            if (param.IsOptional)
                paramText += " = " + param.DefaultValue;

            var paramWidth = r.MeasureText(paramText).X + 20; // 20 for indent
            maxWidth = Math.Max(maxWidth, paramWidth);
        }

        // Closing paren and return type line
        if (currentSignature.Parameters.Count > 0)
        {
            var closingText = ")";
            if (!string.IsNullOrEmpty(currentSignature.ReturnType))
                closingText += " : " + currentSignature.ReturnType;

            var closingWidth = r.MeasureText(closingText).X;
            maxWidth = Math.Max(maxWidth, closingWidth);
        }
        else if (!string.IsNullOrEmpty(currentSignature.ReturnType))
        {
            // Return type on same line as method name
            var returnText = " : " + currentSignature.ReturnType;
            var totalWidth = r.MeasureText(methodNameText + returnText).X;
            maxWidth = Math.Max(maxWidth, totalWidth);
        }

        // Overload counter
        if (_hintInfo.Overloads.Count > 1)
        {
            var counterText = $"[{_hintInfo.CurrentOverloadIndex + 1}/{_hintInfo.Overloads.Count}]";
            var counterWidth = r.MeasureText(counterText).X + 40; // Extra space
            maxWidth = Math.Max(maxWidth, counterWidth);
        }

        return maxWidth + Padding * 2;
    }
}

