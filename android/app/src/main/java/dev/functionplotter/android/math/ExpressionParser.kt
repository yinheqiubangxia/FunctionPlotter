package dev.functionplotter.android.math

import kotlin.math.*

/**
 * 表达式解析器：分词 → 调度场算法 → RPN 求值。
 *
 * 支持：
 * - 四则运算、幂（^，右结合）、一元正负号、百分号
 * - 隐式乘法：2x、2(x+1)、(x+1)(x-1)、x sin(x)
 * - 函数：sin cos tan asin acos atan sinh cosh tanh exp ln log lg sqrt abs
 *         floor ceil round sign min max pow
 * - 常量：pi e tau phi
 * - 绝对值：|x|
 * - 输入容错：全角括号/运算符、f(x)= 前缀、× ÷ −
 *
 * 解析失败抛 [ParseException]；求值时未定义（如 ln(-1)、1/0）返回 NaN，
 * 由采样层负责把 NaN 区间断开。
 */
object ExpressionParser {

    class ParseException(message: String, val position: Int = -1) : Exception(message)

    // ---------- 公共 API ----------

    /** 编译表达式为可重复求值的函数。 */
    fun compile(raw: String): (Double) -> Double {
        val normalized = normalize(raw)
        val tokens = tokenize(normalized)
        val rpn = toRpn(tokens)
        return { x -> evalRpn(rpn, x) }
    }

    /** 校验表达式是否合法，返回错误信息（null 表示合法）。 */
    fun validate(raw: String): String? =
        try {
            compile(raw); null
        } catch (e: ParseException) {
            e.message
        }

    // ---------- 预处理 ----------

    private fun normalize(raw: String): String {
        var s = raw.trim()
        // 去掉 f(x)= / y= 前缀
        s = s.replace(Regex("^[fFyY]\\s*(\\(\\s*[xX]\\s*\\))?\\s*="), "")
        // 全角 → 半角
        s = s.replace('（', '(').replace('）', ')')
            .replace('×', '*').replace('÷', '/').replace('−', '-')
            .replace('，', ',').replace('【', '(').replace('】', ')')
        return s
    }

    // ---------- 分词 ----------

    private sealed interface Token {
        data class Num(val v: Double) : Token
        data class Ident(val name: String) : Token
        data class Op(val c: Char) : Token
        data object LParen : Token
        data object RParen : Token
        data object Pipe : Token
        data object Comma : Token
    }

    private fun tokenize(s: String): List<Token> {
        val out = ArrayList<Token>(s.length)
        var i = 0
        while (i < s.length) {
            val c = s[i]
            when {
                c.isWhitespace() -> i++
                c.isDigit() || c == '.' -> {
                    var j = i
                    while (j < s.length && (s[j].isDigit() || s[j] == '.')) j++
                    if (j < s.length && (s[j] == 'e' || s[j] == 'E') &&
                        j + 1 < s.length && (s[j + 1].isDigit() ||
                            ((s[j + 1] == '+' || s[j + 1] == '-') && j + 2 < s.length && s[j + 2].isDigit()))
                    ) {
                        j++
                        if (s[j] == '+' || s[j] == '-') j++
                        while (j < s.length && s[j].isDigit()) j++
                    }
                    val text = s.substring(i, j)
                    out += Token.Num(text.toDoubleOrNull()
                        ?: throw ParseException("无法解析数字 \"$text\"", i))
                    i = j
                }
                c.isLetter() || c == '_' -> {
                    var j = i
                    while (j < s.length && (s[j].isLetterOrDigit() || s[j] == '_')) j++
                    out += Token.Ident(s.substring(i, j).lowercase())
                    i = j
                }
                c in "+-*/^%" -> { out += Token.Op(c); i++ }
                c == '(' -> { out += Token.LParen; i++ }
                c == ')' -> { out += Token.RParen; i++ }
                c == '|' -> { out += Token.Pipe; i++ }
                c == ',' -> { out += Token.Comma; i++ }
                else -> throw ParseException("无法识别的字符 '$c'", i)
            }
        }
        return insertImplicitMultiplication(out)
    }

    /** 在「值/右括号/闭合管道」后紧跟「数/函数/左括号/开启管道」的位置插入隐式乘法。 */
    private fun insertImplicitMultiplication(tokens: List<Token>): List<Token> {
        if (tokens.size < 2) return tokens
        // 预判每个 Pipe 是开启还是闭合（与解析阶段同一规则：前一个 token 是值则为闭合）
        val pipeIsOpening = BooleanArray(tokens.size)
        var prevIsValue = false
        for (idx in tokens.indices) {
            when (val t = tokens[idx]) {
                is Token.Pipe -> { pipeIsOpening[idx] = !prevIsValue; prevIsValue = !prevIsValue && true || prevIsValue; prevIsValue = pipeIsOpening[idx].not() && true; prevIsValue = !pipeIsOpening[idx] }
                else -> prevIsValue = t is Token.Num || t is Token.RParen ||
                    (t is Token.Ident && (t.name == "x" || t.name in CONSTANTS))
            }
        }
        val out = ArrayList<Token>(tokens.size + 4)
        for (idx in tokens.indices) {
            val t = tokens[idx]
            out += t
            if (idx == tokens.lastIndex) break
            val n = tokens[idx + 1]
            val leftIsValue = t is Token.Num || t is Token.RParen ||
                (t is Token.Ident && (t.name == "x" || t.name in CONSTANTS)) ||
                (t is Token.Pipe && !pipeIsOpening[idx])
            val rightStartsValue = n is Token.Num || n is Token.LParen ||
                (n is Token.Pipe && pipeIsOpening[idx + 1]) ||
                (n is Token.Ident && (n.name == "x" || n.name in CONSTANTS || n.name in FUNCTIONS))
            if (leftIsValue && rightStartsValue) out += Token.Op('*')
        }
        return out
    }

    // ---------- 调度场 → RPN ----------

    private sealed interface Node {
        data class Num(val v: Double) : Node
        data object VarX : Node
        data class Unary(val negate: Boolean, val percent: Boolean) : Node
        data class Bin(val op: Char) : Node
        data class Func(val name: String, val argc: Int) : Node
    }

    private fun toRpn(tokens: List<Token>): List<Node> {
        val out = ArrayList<Node>()
        val ops = ArrayDeque<Any>() // Char 运算符 / "neg" / "abs" / ( / | / FuncCall
        data class FuncCall(val name: String, var argc: Int)
        val funcStack = ArrayDeque<FuncCall>()

        var prevIsValue = false // 上一个 token 是否是"值"，用于区分一元负号
        var i = 0
        for (t in tokens) {
            when (t) {
                is Token.Num -> { out += Node.Num(t.v); prevIsValue = true }
                is Token.Ident -> {
                    when (t.name) {
                        "x" -> { out += Node.VarX; prevIsValue = true }
                        in CONSTANTS -> { out += Node.Num(CONSTANTS.getValue(t.name)); prevIsValue = true }
                        in FUNCTIONS -> {
                            ops.addLast(FuncMarker(t.name)); prevIsValue = false
                        }
                        else -> throw ParseException("未知的名称 \"${t.name}\"")
                    }
                }
                is Token.Op -> {
                    var c = t.c
                    if (c == '%' && prevIsValue) { out += Node.Unary(negate = false, percent = true); continue }
                    if ((c == '-' || c == '+') && !prevIsValue) {
                        if (c == '-') ops.addLast(NEG)
                        prevIsValue = false
                        continue
                    }
                    val prec = PRECEDENCE[c] ?: throw ParseException("不支持的运算符 '$c'")
                    while (ops.isNotEmpty()) {
                        val top = ops.last()
                        when (top) {
                            // 一元负号优先级低于幂：-2^2 = -(2^2) = -4
                            NEG -> if (c == '^') break
                            else { out += Node.Unary(negate = true, percent = false); ops.removeLast() }
                            is Char -> {
                                val p2 = PRECEDENCE[top] ?: break // '(' 或 '|' 停止弹栈
                                if (p2 > prec || (p2 == prec && c != '^')) {
                                    out += Node.Bin(top); ops.removeLast()
                                } else break
                            }
                            else -> break
                        }
                    }
                    ops.addLast(c)
                    prevIsValue = false
                }
                Token.LParen -> {
                    ops.addLast('(')
                    if (ops.size >= 2 && ops[ops.size - 2] is FuncMarker) {
                        funcStack.addLast(FuncCall((ops[ops.size - 2] as FuncMarker).name, 0))
                    }
                    prevIsValue = false
                }
                Token.RParen -> {
                    while (ops.isNotEmpty() && ops.last() != '(') popOp(ops, out)
                    if (ops.isEmpty()) throw ParseException("括号不匹配")
                    ops.removeLast() // '('
                    if (ops.isNotEmpty() && ops.last() is FuncMarker) {
                        ops.removeLast()
                        val fc = funcStack.removeLast()
                        fc.argc++
                        out += Node.Func(fc.name, fc.argc)
                    }
                    prevIsValue = true
                }
                Token.Pipe -> {
                    // 管道既可能是 |x| 的开也可能是闭：当前是值则视为闭合
                    if (prevIsValue && ops.contains('|')) {
                        while (ops.isNotEmpty() && ops.last() != '|') popOp(ops, out)
                        if (ops.isEmpty()) throw ParseException("绝对值符号不匹配")
                        ops.removeLast()
                        out += Node.Func("abs", 1)
                        prevIsValue = true
                    } else {
                        ops.addLast('|')
                        prevIsValue = false
                    }
                }
                Token.Comma -> {
                    while (ops.isNotEmpty() && ops.last() != '(') popOp(ops, out)
                    if (funcStack.isNotEmpty()) funcStack.last().argc++
                    prevIsValue = false
                }
            }
            i++
        }
        while (ops.isNotEmpty()) {
            when (val top = ops.removeLast()) {
                '(' -> throw ParseException("括号不匹配：缺少 ')'")
                '|' -> throw ParseException("绝对值符号不匹配：缺少 '|'")
                else -> popOpAt(top, out)
            }
        }
        if (out.isEmpty()) throw ParseException("表达式为空")
        return out
    }

    private class FuncMarker(val name: String)
    private object NEG

    private fun popOp(ops: ArrayDeque<Any>, out: MutableList<Node>) {
        popOpAt(ops.removeLast(), out)
    }

    private fun popOpAt(top: Any, out: MutableList<Node>) {
        when (top) {
            NEG -> out += Node.Unary(negate = true, percent = false)
            is Char -> out += Node.Bin(top)
            is FuncMarker -> out += Node.Func(top.name, 1)
            else -> throw ParseException("内部错误")
        }
    }

    // ---------- RPN 求值 ----------

    private fun evalRpn(rpn: List<Node>, x: Double): Double {
        val stack = DoubleArray(rpn.size)
        var sp = 0
        for (n in rpn) {
            when (n) {
                is Node.Num -> stack[sp++] = n.v
                Node.VarX -> stack[sp++] = x
                is Node.Unary -> {
                    if (sp < 1) return Double.NaN
                    var v = stack[--sp]
                    if (n.negate) v = -v
                    if (n.percent) v /= 100.0
                    stack[sp++] = v
                }
                is Node.Bin -> {
                    if (sp < 2) return Double.NaN
                    val b = stack[--sp]; val a = stack[--sp]
                    stack[sp++] = when (n.op) {
                        '+' -> a + b
                        '-' -> a - b
                        '*' -> a * b
                        '/' -> a / b
                        '^' -> a.pow(b)
                        else -> Double.NaN
                    }
                }
                is Node.Func -> {
                    if (sp < n.argc) return Double.NaN
                    sp -= n.argc
                    // 注意：不能写成 stack[sp++] = applyFunc(..., sp, ...) —
                    // 下标 sp++ 先求值会使 RHS 读到 +1 后的 sp
                    val r = applyFunc(n.name, stack, sp, n.argc)
                    stack[sp++] = r
                }
            }
        }
        return if (sp == 1) stack[0] else Double.NaN
    }

    private fun applyFunc(name: String, args: DoubleArray, off: Int, argc: Int): Double {
        val a = args[off]
        return when (name) {
            "sin" -> sin(a)
            "cos" -> cos(a)
            "tan" -> tan(a)
            "asin" -> asin(a)
            "acos" -> acos(a)
            "atan" -> atan(a)
            "sinh" -> sinh(a)
            "cosh" -> cosh(a)
            "tanh" -> tanh(a)
            "exp" -> exp(a)
            "ln" -> ln(a)
            "log" -> if (argc == 2) log(args[off + 1], a) else log10(a)
            "lg" -> log10(a)
            "sqrt" -> sqrt(a)
            "abs" -> abs(a)
            "floor" -> floor(a)
            "ceil" -> ceil(a)
            "round" -> round(a).toDouble()
            "sign" -> sign(a)
            "min" -> if (argc == 2) min(a, args[off + 1]) else Double.NaN
            "max" -> if (argc == 2) max(a, args[off + 1]) else Double.NaN
            "pow" -> if (argc == 2) a.pow(args[off + 1]) else Double.NaN
            else -> Double.NaN
        }
    }

    private val CONSTANTS = mapOf(
        "pi" to PI, "e" to E, "tau" to 2 * PI, "phi" to (1 + sqrt(5.0)) / 2
    )

    private val FUNCTIONS = setOf(
        "sin", "cos", "tan", "asin", "acos", "atan", "sinh", "cosh", "tanh",
        "exp", "ln", "log", "lg", "sqrt", "abs", "floor", "ceil", "round",
        "sign", "min", "max", "pow"
    )

    private val PRECEDENCE = mapOf('+' to 1, '-' to 1, '*' to 2, '/' to 2, '^' to 4)
}
