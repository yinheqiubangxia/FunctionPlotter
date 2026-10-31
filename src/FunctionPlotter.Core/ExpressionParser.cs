using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FunctionPlotter.Core
{
    /// <summary>
    /// 表达式语法错误。Position 为出错位置（从 0 开始），便于 UI 定位。
    /// </summary>
    public sealed class ExpressionException : Exception
    {
        public ExpressionException(string message, int position) : base(message)
        {
            Position = position;
        }

        public int Position { get; }
    }

    /// <summary>
    /// 编译后的函数表达式，可反复求值，不含任何 UI 依赖。
    /// </summary>
    public sealed class CompiledExpression
    {
        private readonly Node _root;

        internal CompiledExpression(string text, Node root, bool usesVariable)
        {
            Text = text;
            _root = root;
            UsesVariable = usesVariable;
        }

        /// <summary>用户输入的原始文本（已规范化空白）。</summary>
        public string Text { get; }

        /// <summary>表达式中是否真的用到了自变量 x。</summary>
        public bool UsesVariable { get; }

        public double Evaluate(double x) => _root.Eval(x);

        public override string ToString() => Text;
    }

    /// <summary>
    /// 把 "sin(x)/x" 这类文本编译为 <see cref="CompiledExpression"/>。
    /// 支持：+ - * / ^、隐式乘法（2x、3(x+1)、(x+1)(x-1)）、括号、|x|、
    /// 常用初等函数与常量 pi/π、e、tau。
    /// </summary>
    public static class ExpressionCompiler
    {
        public static CompiledExpression Compile(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            string source = Normalize(text);
            if (source.Trim().Length == 0)
                throw new ExpressionException("请输入函数表达式", 0);

            List<Token> tokens = Lexer.Tokenize(source);
            var parser = new Parser(tokens);
            Node root = parser.Parse();
            return new CompiledExpression(source.Trim(), root, parser.UsesVariable);
        }

        /// <summary>把常见的全角/数学符号统一成 ASCII，避免用户从 Word 里粘贴出错。</summary>
        private static string Normalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '×':
                    case '·':
                    case '⋅':
                        sb.Append('*'); break;
                    case '÷':
                        sb.Append('/'); break;
                    case '−':
                    case '–':
                    case '—':
                        sb.Append('-'); break;
                    case '（':
                        sb.Append('('); break;
                    case '）':
                        sb.Append(')'); break;
                    case '，':
                        sb.Append(','); break;
                    case '＾':
                        sb.Append('^'); break;
                    case '＋':
                        sb.Append('+'); break;
                    case '＝':
                        sb.Append('='); break;
                    case '\u00A0':
                    case '\u3000':
                        sb.Append(' '); break;
                    default:
                        sb.Append(c); break;
                }
            }

            string s = sb.ToString();

            // 允许用户输入 "y=" / "f(x)=" 前缀，直接忽略
            int eq = s.IndexOf('=');
            if (eq >= 0) s = s.Substring(eq + 1);

            return s;
        }
    }

    internal enum TokenKind
    {
        Number,
        Identifier,
        Operator,
        LeftParen,
        RightParen,
        Comma,
        End
    }

    internal struct Token
    {
        public TokenKind Kind;
        public double Number;
        public string Text;
        public char Symbol;
        public int Position;
    }

    internal static class Lexer
    {
        // 少数函数名本身带数字（log2、log10、atan2），只有拼成完整函数名时才吞掉数字
        private static readonly HashSet<string> DigitSuffixedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "log2", "log10", "atan2"
        };

        public static List<Token> Tokenize(string s)
        {
            var tokens = new List<Token>();
            int i = 0;

            while (i < s.Length)
            {
                char c = s[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    int start = i;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                    if (i < s.Length && s[i] == '.')
                    {
                        i++;
                        while (i < s.Length && char.IsDigit(s[i])) i++;
                    }

                    // 科学计数法：1e3、2.5E-4（仅当 e 后面确实跟着数字时才吞掉）
                    if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                    {
                        int save = i;
                        i++;
                        if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                        if (i < s.Length && char.IsDigit(s[i]))
                        {
                            while (i < s.Length && char.IsDigit(s[i])) i++;
                        }
                        else
                        {
                            i = save;
                        }
                    }

                    string span = s.Substring(start, i - start);
                    if (!double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                        throw new ExpressionException($"无法识别的数字：\"{span}\"", start);

                    tokens.Add(new Token { Kind = TokenKind.Number, Number = value, Text = span, Position = start });
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    // 标识符只含字母与下划线；数字留给隐式乘法处理（x2 => x*2）
                    int start = i;
                    while (i < s.Length && (char.IsLetter(s[i]) || s[i] == '_')) i++;
                    string name = s.Substring(start, i - start);
                    while (i < s.Length && char.IsDigit(s[i]) && DigitSuffixedNames.Contains(name + s[i]))
                    {
                        name += s[i];
                        i++;
                    }
                    tokens.Add(new Token { Kind = TokenKind.Identifier, Text = name, Position = start });
                    continue;
                }

                switch (c)
                {
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                    case '^':
                    case '|':
                        tokens.Add(new Token { Kind = TokenKind.Operator, Symbol = c, Text = c.ToString(), Position = i });
                        i++;
                        continue;
                    case '(':
                        tokens.Add(new Token { Kind = TokenKind.LeftParen, Text = "(", Position = i });
                        i++;
                        continue;
                    case ')':
                        tokens.Add(new Token { Kind = TokenKind.RightParen, Text = ")", Position = i });
                        i++;
                        continue;
                    case ',':
                        tokens.Add(new Token { Kind = TokenKind.Comma, Text = ",", Position = i });
                        i++;
                        continue;
                }

                throw new ExpressionException($"无法识别的字符：\"{c}\"", i);
            }

            tokens.Add(new Token { Kind = TokenKind.End, Text = "<结束>", Position = s.Length });
            return tokens;
        }
    }

    internal sealed class Parser
    {
        private static readonly Dictionary<string, double> Constants = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            { "pi", Math.PI },
            { "π", Math.PI },
            { "e", Math.E },
            { "tau", Math.PI * 2.0 },
            { "phi", (1.0 + Math.Sqrt(5.0)) / 2.0 },
        };

        private static readonly Dictionary<string, Func<double, double>> UnaryFunctions = new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
        {
            { "sin", Math.Sin },
            { "cos", Math.Cos },
            { "tan", Math.Tan },
            { "cot", x => 1.0 / Math.Tan(x) },
            { "sec", x => 1.0 / Math.Cos(x) },
            { "csc", x => 1.0 / Math.Sin(x) },
            { "asin", Math.Asin },
            { "acos", Math.Acos },
            { "atan", Math.Atan },
            { "sinh", Math.Sinh },
            { "cosh", Math.Cosh },
            { "tanh", Math.Tanh },
            { "asinh", x => Math.Log(x + Math.Sqrt(x * x + 1.0)) },
            { "acosh", x => Math.Log(x + Math.Sqrt(x * x - 1.0)) },
            { "atanh", x => 0.5 * Math.Log((1.0 + x) / (1.0 - x)) },
            { "exp", Math.Exp },
            { "ln", Math.Log },
            { "log", Math.Log10 },
            { "log2", x => Math.Log(x, 2.0) },
            { "log10", Math.Log10 },
            { "lg", Math.Log10 },
            { "sqrt", Math.Sqrt },
            { "cbrt", x => Math.Pow(x, 1.0 / 3.0) },
            { "abs", Math.Abs },
            { "sign", x => Math.Sign(x) },
            { "floor", Math.Floor },
            { "ceil", Math.Ceiling },
            { "round", x => Math.Round(x, MidpointRounding.AwayFromZero) },
            { "trunc", Math.Truncate },
            { "frac", x => x - Math.Truncate(x) },
            { "rad", x => x * Math.PI / 180.0 },
            { "deg", x => x * 180.0 / Math.PI },
        };

        private static readonly Dictionary<string, Func<double, double, double>> BinaryFunctions = new Dictionary<string, Func<double, double, double>>(StringComparer.OrdinalIgnoreCase)
        {
            { "pow", Math.Pow },
            { "atan2", Math.Atan2 },
            { "hypot", (a, b) => Math.Sqrt(a * a + b * b) },
            { "max", Math.Max },
            { "min", Math.Min },
            { "mod", (a, b) => a - b * Math.Floor(a / b) },
            { "log", (a, b) => Math.Log(a, b) },
        };

        private readonly List<Token> _tokens;
        private int _index;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _index = 0;
        }

        public bool UsesVariable { get; private set; }

        private Token Current => _tokens[_index];

        private Token Advance()
        {
            Token t = _tokens[_index];
            if (_index < _tokens.Count - 1) _index++;
            return t;
        }

        public Node Parse()
        {
            Node node = ParseExpression();
            if (Current.Kind != TokenKind.End)
            {
                if (Current.Kind == TokenKind.RightParen)
                    throw new ExpressionException("多余的右括号", Current.Position);
                throw new ExpressionException($"这里看不懂：\"{Current.Text}\"", Current.Position);
            }
            return node;
        }

        private Node ParseExpression()
        {
            Node left = ParseTerm();
            while (true)
            {
                if (MatchOperator('+'))
                {
                    left = new BinaryNode(BinaryOperator.Add, left, ParseTerm());
                }
                else if (MatchOperator('-'))
                {
                    left = new BinaryNode(BinaryOperator.Subtract, left, ParseTerm());
                }
                else
                {
                    return left;
                }
            }
        }

        private Node ParseTerm()
        {
            Node left = ParseUnary();
            while (true)
            {
                if (MatchOperator('*'))
                {
                    left = new BinaryNode(BinaryOperator.Multiply, left, ParseUnary());
                }
                else if (MatchOperator('/'))
                {
                    left = new BinaryNode(BinaryOperator.Divide, left, ParseUnary());
                }
                else if (StartsImplicitFactor())
                {
                    // 隐式乘法：2x、3(x+1)、(x+1)(x-1)、2 sin(x)
                    left = new BinaryNode(BinaryOperator.Multiply, left, ParseUnary());
                }
                else
                {
                    return left;
                }
            }
        }

        private bool StartsImplicitFactor()
        {
            switch (Current.Kind)
            {
                case TokenKind.Number:
                case TokenKind.Identifier:
                case TokenKind.LeftParen:
                    return true;
                default:
                    return false;
            }
        }

        private Node ParseUnary()
        {
            if (MatchOperator('-')) return new NegateNode(ParseUnary());
            if (MatchOperator('+')) return ParseUnary();
            return ParsePower();
        }

        private Node ParsePower()
        {
            Node baseNode = ParseAtom();
            if (MatchOperator('^'))
            {
                // 右结合：2^3^2 = 2^(3^2)
                return new BinaryNode(BinaryOperator.Power, baseNode, ParseUnary());
            }
            return baseNode;
        }

        private Node ParseAtom()
        {
            Token t = Current;

            if (t.Kind == TokenKind.Number)
            {
                Advance();
                return new ConstantNode(t.Number);
            }

            if (t.Kind == TokenKind.LeftParen)
            {
                Advance();
                Node inner = ParseExpression();
                if (Current.Kind != TokenKind.RightParen)
                    throw new ExpressionException("缺少右括号 \")\"", Current.Position);
                Advance();
                return inner;
            }

            if (t.Kind == TokenKind.Operator && t.Symbol == '|')
            {
                Advance();
                Node inner = ParseExpression();
                if (!(Current.Kind == TokenKind.Operator && Current.Symbol == '|'))
                    throw new ExpressionException("绝对值缺少收尾的 \"|\"", Current.Position);
                Advance();
                return new UnaryCallNode(Math.Abs, inner);
            }

            if (t.Kind == TokenKind.Identifier)
            {
                string name = t.Text;
                Advance();

                bool isCall = Current.Kind == TokenKind.LeftParen;
                if (isCall && (UnaryFunctions.ContainsKey(name) || BinaryFunctions.ContainsKey(name)))
                {
                    return ParseCall(name);
                }

                if (string.Equals(name, "x", StringComparison.OrdinalIgnoreCase))
                {
                    UsesVariable = true;
                    return VariableNode.Instance;
                }

                if (Constants.TryGetValue(name, out double constant))
                {
                    if (isCall)
                        throw new ExpressionException($"\"{name}\" 不是函数", t.Position);
                    return new ConstantNode(constant);
                }

                // 常见拼写兜底提示
                string? hint = ClosestFunctionHint(name);
                throw new ExpressionException(
                    hint != null ? $"未知的函数或变量：\"{name}\"（是不是 {hint}？）" : $"未知的函数或变量：\"{name}\"",
                    t.Position);
            }

            if (t.Kind == TokenKind.RightParen)
                throw new ExpressionException("多了个右括号，或运算符缺失", t.Position);

            if (t.Kind == TokenKind.End)
                throw new ExpressionException("表达式不完整，后面还需要内容", t.Position);

            throw new ExpressionException($"这里需要一个数值、变量或函数，而不是 \"{t.Text}\"", t.Position);
        }

        private Node ParseCall(string name)
        {
            Advance(); // 吃掉 '('

            var args = new List<Node>();
            if (Current.Kind != TokenKind.RightParen)
            {
                args.Add(ParseExpression());
                while (Current.Kind == TokenKind.Comma)
                {
                    Advance();
                    args.Add(ParseExpression());
                }
            }

            if (Current.Kind != TokenKind.RightParen)
                throw new ExpressionException($"函数 {name}( ) 缺少右括号", Current.Position);
            Advance();

            if (UnaryFunctions.TryGetValue(name, out Func<double, double> unary) && args.Count == 1)
                return new UnaryCallNode(unary, args[0]);

            if (BinaryFunctions.TryGetValue(name, out Func<double, double, double> binary) && args.Count == 2)
                return new BinaryCallNode(binary, args[0], args[1]);

            // log 允许一个或两个参数
            if (string.Equals(name, "log", StringComparison.OrdinalIgnoreCase) && args.Count == 1)
                return new UnaryCallNode(Math.Log10, args[0]);

            string expected = BinaryFunctions.ContainsKey(name)
                ? (UnaryFunctions.ContainsKey(name) ? "1 个或 2 个" : "2 个")
                : "1 个";
            throw new ExpressionException($"函数 {name} 需要 {expected}参数，实际给了 {args.Count} 个", Current.Position);
        }

        private static string? ClosestFunctionHint(string name)
        {
            string? best = null;
            int bestDistance = int.MaxValue;
            foreach (string candidate in UnaryFunctions.Keys)
            {
                int d = Levenshtein(name.ToLowerInvariant(), candidate);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }

            return bestDistance <= 2 ? best : null;
        }

        private static int Levenshtein(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                Array.Copy(cur, prev, cur.Length);
            }

            return prev[b.Length];
        }

        private bool MatchOperator(char symbol)
        {
            if (Current.Kind == TokenKind.Operator && Current.Symbol == symbol)
            {
                Advance();
                return true;
            }
            return false;
        }
    }
}
