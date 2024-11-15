using System;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Lumina.Text;
using Lumina.Text.ReadOnly;
using Lumina.Text.Payloads;
using SixLabors.ImageSharp.Formats;
using System.Xml.XPath;

public class SeStringConverter : JsonConverter<ReadOnlySeString>
{
    private ExcelSheet<UIColor> _uiColors;

    public SeStringConverter(ExcelSheet<UIColor> uiColors)
    {
        
        _uiColors = uiColors;
    }

    public override ReadOnlySeString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For deserialization, you might want to convert the JSON string back to SeString.
        // This example assumes you're not deserializing SeString objects.
        throw new NotImplementedException("Deserialization is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, ReadOnlySeString value, JsonSerializerOptions options)
    {
        // Serialize the SeString object as a JSON string.
        try { writer.WriteStringValue(ConvertSeString(value)); } catch (Exception) {
            writer.WriteNullValue();
        }
    }

    public String ConvertSeString(ReadOnlySeString value)
    {
        return string.Join("", value.Select(ConvertPayload));
    }

    public string ConvertPayloadTag(String type)
    {
        // Convert italics to HTML <i> and </i> tags.
        var lowerType = type.ToLower();
        if (lowerType == "<italics>1</italics>")
            return "<i>";
        if (lowerType == "<italics>0</italics>")
            return "</i>";

        // Eliminate uicolorborder ("UIGlow") as we don't bother representing it in any way.
        if (lowerType.StartsWith("<edgecolortype")) {
            return "";
        }

        // Convert ui color fill to a span with a foreground color.
        if (lowerType.StartsWith("<colortype")) {
            if (lowerType == "<colortype>0</colortype>")
                return "</span>";
            string numPattern = @"<colortype>(\d+)</colortype>";
        
            Match match = Regex.Match(lowerType, numPattern);
            string colorAttr = "";
            if (match.Success) {
                string numStr = match.Groups[1].Value; // Extract the number as a string
                uint num = uint.Parse(numStr); // Convert the string to an integer
                var row = _uiColors.GetRow(num);
                
                var colorProperty = typeof(UIColor).GetProperty("UIForeground");
                if (colorProperty != null && colorProperty.PropertyType == typeof(uint)) {
                    uint colorValue = (uint)colorProperty.GetValue(row);
                    string hexValue = colorValue.ToString("x");
                    string paddedHexValue = hexValue.PadLeft(8, '0');
                    string firstSixChars = paddedHexValue.Substring(0, 6);
                    colorAttr = " style=\"color: #" + firstSixChars + ";\"";
                }
            }

            return $"<span{colorAttr}>";
        }
    
        return type;
    }

    public String ConvertExpression(Lumina.Text.ReadOnly.ReadOnlySeExpression expression)
    {
        return expression.AsSpan().ToString();
        /*
        if (expression is Lumina.Text.Expressions.StringExpression se)
            return ConvertSeString(se.Value);
        if (expression is Lumina.Text.Expressions.BinaryExpression be)
            return ConvertBinaryExpression(be);
         if (expression is Lumina.Text.Expressions.ParameterExpression pe)
            return ConvertParameterExpression(pe);
        return expression.ToString();
        */
    }

    /*public String ConvertParameterExpression(Lumina.Text.Expressions.ParameterExpression expression)
    {
        return expression.ExpressionType switch
        {
            Lumina.Text.Expressions.ExpressionType.IntegerParameter => $"IntegerParameter({ConvertExpression(expression.Operand)})",
            Lumina.Text.Expressions.ExpressionType.PlayerParameter => $"PlayerParameter({ConvertExpression(expression.Operand)})",
            Lumina.Text.Expressions.ExpressionType.StringParameter => $"StringParameter({ConvertExpression(expression.Operand)})",
            Lumina.Text.Expressions.ExpressionType.ObjectParameter => $"ObjectParameter({ConvertExpression(expression.Operand)})",
            _ => throw new NotImplementedException() // cannot reach, as this instance is immutable and this field is filtered from constructor
        };
    }

    public String ConvertBinaryExpression(Lumina.Text.Expressions.BinaryExpression expression)
    {
        var first = ConvertExpression(expression.Operand1);
        var second = ConvertExpression(expression.Operand2);
        
        var result = expression.ExpressionType switch
        {
            Lumina.Text.Expressions.ExpressionType.GreaterThanOrEqualTo => $"GreaterThanOrEqualTo({first},{second})",
            Lumina.Text.Expressions.ExpressionType.GreaterThan => $"GreaterThan({first},{second})",
            Lumina.Text.Expressions.ExpressionType.LessThanOrEqualTo => $"LessThanOrEqualTo({first},{second})",
            Lumina.Text.Expressions.ExpressionType.LessThan => $"LessThan({first},{second})",
            Lumina.Text.Expressions.ExpressionType.Equal => $"Equal({first},{second})",
            Lumina.Text.Expressions.ExpressionType.NotEqual => $"NotEqual({first},{second})",
            _ => throw new NotImplementedException() // cannot reach, as this instance is immutable and this field is filtered from constructor
        };

        // Define the regular expression pattern
         // \b matches a word boundary, gnum matches the literal string "gnum",
        // \d+ matches one or more digits, and (?=\D) is a positive lookahead that asserts the next character is not a digit
        string pattern = @"\bgnum(\d+)(?=\D)";

        // Define the replacement string using a MatchEvaluator delegate
        // This allows us to dynamically construct the replacement string based on the matched digits
        return Regex.Replace(result, pattern, match => $"PlayerParameter({match.Groups[1].Value})");
    }
*/

    public String ConvertPayload(ReadOnlySePayload payload)
    {
        if (payload.Type == ReadOnlySePayloadType.Invalid)
            return "";

        if (payload.Type == ReadOnlySePayloadType.Text)
            return payload.AsSpan().ToString().Replace( "<", "\\<" );

        switch( payload.MacroCode )
            {
                case MacroCode.NewLine:
                    return "\n";
                case MacroCode.SoftHyphen:
                    return "";
                case MacroCode.Hyphen:
                    return "–";
                default:
                {
                    //return "\nExpression: " + payload.AsSpan().ToString() + "\n";

                    var expressions = (payload as ICollection<ReadOnlySeExpression>);
                    var expressionCount = (payload as ICollection<ReadOnlySeExpression>).Count;
                  
                    if (expressionCount == 0)
                        return ConvertPayloadTag($"<{payload.AsSpan().ToString().ToLower()}>");

                    var originalPayloadTag = payload.MacroCode.ToString();
                    var payloadTag = originalPayloadTag.ToLower();
                 
                    //return payloadTag + ":" + expressionCount;

                    if (payloadTag == "if" && expressionCount == 3) {
                        string expression1 = ConvertExpression(expressions.ElementAt(0));
                        string expression2 = ConvertExpression(expressions.ElementAt(1));
                        if (expression2.Length == 0)
                            expression2 = " ";
                        string expression3 = ConvertExpression(expressions.ElementAt(2));
                        if (expression3.Length == 0)
                            expression3 = " ";
                        return $"<If({expression1})>{expression2}<Else/>{expression3}</If>";
                    }
                    
                    if (expressions.Count == 1) {
                        string expression1 = ConvertExpression(expressions.ElementAt(0));
                        return ConvertPayloadTag($"<{originalPayloadTag}>{expression1}</{originalPayloadTag}>");
                    }

                    return ConvertPayloadTag($"<{payloadTag}({string.Join( ',', expressions.Select(
                        ex => ConvertExpression(ex)
                    ) )})>");
                }
            }
    }
}