using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpawnGuide
{
    /// <summary>
    /// Simple mathematical expression evaluator for config parameters.
    /// Supports: + - * / ( ) min max clamp abs sin cos sqrt pow log, constants, variable references.
    /// Example: "min(10, max(5, x * 1.2))" or "clamp(base * density, 0, 100)"
    /// </summary>
    [JsonConverter(typeof(ExpressionConverter))]
    public sealed class Expression : IEquatable<Expression>
    {
        private readonly string _text;
        private readonly ExprNode _ast;
        private readonly HashSet<string> _variables;

        public string Text => _text;
        public IReadOnlySet<string> Variables => _variables;

        public Expression(string text)
        {
            _text = text?.Trim() ?? "";
            _variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _ast = string.IsNullOrEmpty(_text) ? new ConstNode(0) : Parse(_text);
        }

        /// <summary>Evaluate the expression with the given variable values.</summary>
        public float Evaluate(IDictionary<string, float> vars = null)
        {
            var ctx = new EvalContext(vars ?? new Dictionary<string, float>());
            return _ast.Eval(ctx);
        }

        /// <summary>Evaluate with a simple lambda for variable lookup.</summary>
        public float Evaluate(Func<string, float> getVar)
        {
            var ctx = new EvalContext(getVar);
            return _ast.Eval(ctx);
        }

        public bool Equals(Expression other) => other != null && _text == other._text;
        public override bool Equals(object obj) => obj is Expression e && Equals(e);
        public override int GetHashCode() => _text.GetHashCode();
        public static bool operator ==(Expression a, Expression b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(Expression a, Expression b) => !(a == b);
        public override string ToString() => _text;

        // -------- Parser --------

        private ExprNode Parse(string text)
        {
            var tokens = Tokenize(text);
            int pos = 0;
            var node = ParseExpression(tokens, ref pos);
            if (pos < tokens.Count) throw new ArgumentException($"Unexpected token at position {pos}: {tokens[pos]}");
            return node;
        }

        private List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c == '(' || c == ')' || c == ',' || c == '+' || c == '-' || c == '*' || c == '/' || c == '^')
                {
                    tokens.Add(new Token(TokenType.Operator, c.ToString()));
                    i++;
                }
                else if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                    tokens.Add(new Token(TokenType.Number, text.Substring(start, i - start)));
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    string ident = text.Substring(start, i - start);
                    // Check if it's a function (followed by '(')
                    int j = i;
                    while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                    if (j < text.Length && text[j] == '(')
                        tokens.Add(new Token(TokenType.Function, ident));
                    else
                    {
                        tokens.Add(new Token(TokenType.Variable, ident));
                        _variables.Add(ident);
                    }
                }
                else
                {
                    throw new ArgumentException($"Invalid character '{c}' at position {i}");
                }
            }
            tokens.Add(new Token(TokenType.End, ""));
            return tokens;
        }

        private ExprNode ParseExpression(List<Token> tokens, ref int pos) => ParseAddSub(tokens, ref pos);

        private ExprNode ParseAddSub(List<Token> tokens, ref int pos)
        {
            var left = ParseMulDiv(tokens, ref pos);
            while (pos < tokens.Count)
            {
                var tok = tokens[pos];
                if (tok.Type == TokenType.Operator && (tok.Text == "+" || tok.Text == "-"))
                {
                    pos++;
                    var right = ParseMulDiv(tokens, ref pos);
                    left = new BinaryOpNode(left, tok.Text[0], right);
                }
                else break;
            }
            return left;
        }

        private ExprNode ParseMulDiv(List<Token> tokens, ref int pos)
        {
            var left = ParsePower(tokens, ref pos);
            while (pos < tokens.Count)
            {
                var tok = tokens[pos];
                if (tok.Type == TokenType.Operator && (tok.Text == "*" || tok.Text == "/" || tok.Text == "%"))
                {
                    pos++;
                    var right = ParsePower(tokens, ref pos);
                    left = new BinaryOpNode(left, tok.Text[0], right);
                }
                else break;
            }
            return left;
        }

        private ExprNode ParsePower(List<Token> tokens, ref int pos)
        {
            var left = ParseUnary(tokens, ref pos);
            while (pos < tokens.Count)
            {
                var tok = tokens[pos];
                if (tok.Type == TokenType.Operator && tok.Text == "^")
                {
                    pos++;
                    var right = ParseUnary(tokens, ref pos);
                    left = new BinaryOpNode(left, '^', right);
                }
                else break;
            }
            return left;
        }

        private ExprNode ParseUnary(List<Token> tokens, ref int pos)
        {
            if (pos < tokens.Count)
            {
                var tok = tokens[pos];
                if (tok.Type == TokenType.Operator && (tok.Text == "+" || tok.Text == "-"))
                {
                    pos++;
                    var operand = ParseUnary(tokens, ref pos);
                    return new UnaryOpNode(tok.Text[0], operand);
                }
            }
            return ParsePrimary(tokens, ref pos);
        }

        private ExprNode ParsePrimary(List<Token> tokens, ref int pos)
        {
            if (pos >= tokens.Count) throw new ArgumentException("Unexpected end of expression");

            var tok = tokens[pos];
            if (tok.Type == TokenType.Number)
            {
                pos++;
                return new ConstNode(float.Parse(tok.Text));
            }
            if (tok.Type == TokenType.Variable)
            {
                pos++;
                return new VariableNode(tok.Text);
            }
            if (tok.Type == TokenType.Function)
            {
                string func = tok.Text;
                pos++; // consume function name
                if (pos >= tokens.Count || tokens[pos].Text != "(") throw new ArgumentException($"Expected '(' after function {func}");
                pos++; // consume '('
                var args = new List<ExprNode>();
                if (tokens[pos].Text != ")")
                {
                    while (true)
                    {
                        args.Add(ParseExpression(tokens, ref pos));
                        if (tokens[pos].Text == ",") pos++;
                        else break;
                    }
                }
                if (pos >= tokens.Count || tokens[pos].Text != ")") throw new ArgumentException("Expected ')'");
                pos++; // consume ')'
                return new FunctionNode(func, args);
            }
            if (tok.Text == "(")
            {
                pos++;
                var expr = ParseExpression(tokens, ref pos);
                if (pos >= tokens.Count || tokens[pos].Text != ")") throw new ArgumentException("Expected ')'");
                pos++;
                return expr;
            }
            throw new ArgumentException($"Unexpected token: {tok.Text}");
        }

        // -------- AST Nodes --------

        private abstract class ExprNode
        {
            public abstract float Eval(EvalContext ctx);
        }

        private sealed class ConstNode : ExprNode
        {
            public readonly float Value;
            public ConstNode(float v) => Value = v;
            public override float Eval(EvalContext ctx) => Value;
        }

        private sealed class VariableNode : ExprNode
        {
            public readonly string Name;
            public VariableNode(string n) => Name = n;
            public override float Eval(EvalContext ctx) => ctx.Get(Name);
        }

        private sealed class BinaryOpNode : ExprNode
        {
            public readonly ExprNode Left, Right;
            public readonly char Op;
            public BinaryOpNode(ExprNode l, char o, ExprNode r) => (Left, Op, Right) = (l, o, r);
            public override float Eval(EvalContext ctx)
            {
                float l = Left.Eval(ctx), r = Right.Eval(ctx);
                return Op switch
                {
                    '+' => l + r,
                    '-' => l - r,
                    '*' => l * r,
                    '/' => r != 0 ? l / r : 0,
                    '%' => r != 0 ? l % r : 0,
                    '^' => MathF.Pow(l, r),
                    _ => 0,
                };
            }
        }

        private sealed class UnaryOpNode : ExprNode
        {
            public readonly ExprNode Operand;
            public readonly char Op;
            public UnaryOpNode(char o, ExprNode e) => (Op, Operand) = (o, e);
            public override float Eval(EvalContext ctx)
            {
                float v = Operand.Eval(ctx);
                return Op == '-' ? -v : v;
            }
        }

        private sealed class FunctionNode : ExprNode
        {
            public readonly string Name;
            public readonly List<ExprNode> Args;
            public FunctionNode(string n, List<ExprNode> a) => (Name, Args) = (n, a);
            public override float Eval(EvalContext ctx)
            {
                var vals = new float[Args.Count];
                for (int i = 0; i < Args.Count; i++) vals[i] = Args[i].Eval(ctx);
                return Name.ToLowerInvariant() switch
                {
                    "min" => vals.Length > 0 ? Min(vals) : 0,
                    "max" => vals.Length > 0 ? Max(vals) : 0,
                    "clamp" => vals.Length == 3 ? Math.Clamp(vals[0], vals[1], vals[2]) : 0,
                    "abs" => vals.Length > 0 ? MathF.Abs(vals[0]) : 0,
                    "sin" => vals.Length > 0 ? MathF.Sin(vals[0]) : 0,
                    "cos" => vals.Length > 0 ? MathF.Cos(vals[0]) : 0,
                    "tan" => vals.Length > 0 ? MathF.Tan(vals[0]) : 0,
                    "sqrt" => vals.Length > 0 ? MathF.Sqrt(MathF.Max(0, vals[0])) : 0,
                    "pow" => vals.Length == 2 ? MathF.Pow(vals[0], vals[1]) : 0,
                    "log" => vals.Length > 0 ? MathF.Log(MathF.Max(float.Epsilon, vals[0])) : 0,
                    "exp" => vals.Length > 0 ? MathF.Exp(vals[0]) : 0,
                    "floor" => vals.Length > 0 ? MathF.Floor(vals[0]) : 0,
                    "ceil" => vals.Length > 0 ? MathF.Ceiling(vals[0]) : 0,
                    "round" => vals.Length > 0 ? MathF.Round(vals[0]) : 0,
                    "lerp" => vals.Length == 3 ? vals[0] + (vals[1] - vals[0]) * Math.Clamp(vals[2], 0, 1) : 0,
                    "smoothstep" => vals.Length == 3 ? SmoothStep(vals[0], vals[1], vals[2]) : 0,
                    _ => 0,
                };
            }

            private float Min(float[] a) { float m = a[0]; for (int i = 1; i < a.Length; i++) if (a[i] < m) m = a[i]; return m; }
            private float Max(float[] a) { float m = a[0]; for (int i = 1; i < a.Length; i++) if (a[i] > m) m = a[i]; return m; }
            private float SmoothStep(float a, float b, float t) { t = Math.Clamp((t - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
        }

        private sealed class EvalContext
        {
            private readonly IDictionary<string, float> _vars;
            private readonly Func<string, float> _getVar;

            public EvalContext(IDictionary<string, float> vars) => _vars = vars;
            public EvalContext(Func<string, float> getVar) => _getVar = getVar;

            public float Get(string name)
            {
                if (_getVar != null) return _getVar(name);
                return _vars.TryGetValue(name, out var v) ? v : 0f;
            }
        }

        // -------- Token --------

        private enum TokenType { Number, Variable, Function, Operator, End }
        private readonly struct Token
        {
            public readonly TokenType Type;
            public readonly string Text;
            public Token(TokenType t, string txt) => (Type, Text) = (t, txt);
        }
    }

    internal class ExpressionConverter : JsonConverter<Expression>
    {
        public override Expression Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return new Expression(reader.GetString());
            if (reader.TokenType == JsonTokenType.Null)
                return new Expression("");
            throw new JsonException("Expression must be a string");
        }

        public override void Write(Utf8JsonWriter writer, Expression value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.Text);
        }
    }
}