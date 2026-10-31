// 独立冒烟测试： kotlinc 直接编译 math/ 与本文件运行（无需 JUnit）。
import dev.functionplotter.android.math.ExpressionParser
import dev.functionplotter.android.math.Sampler
import kotlin.math.abs
import kotlin.math.sqrt

var passed = 0
var failed = 0

fun check(name: String, cond: Boolean) {
    if (cond) { passed++ } else { failed++; println("FAIL: $name") }
}

fun near(a: Double, b: Double, eps: Double = 1e-9) = abs(a - b) < eps

fun main() {
    // 基础运算
    check("1+2*3", near(ExpressionParser.compile("1+2*3")(0.0), 7.0))
    check("(1+2)*3", near(ExpressionParser.compile("(1+2)*3")(0.0), 9.0))
    check("2^3^2 右结合", near(ExpressionParser.compile("2^3^2")(0.0), 512.0))
    check("-2^2 = -4", near(ExpressionParser.compile("-2^2")(0.0), -4.0))
    check("一元负号 -x", near(ExpressionParser.compile("-x")(3.0), -3.0))

    // 变量与隐式乘法
    check("x^2", near(ExpressionParser.compile("x^2")(3.0), 9.0))
    check("2x 隐式乘法", near(ExpressionParser.compile("2x")(4.0), 8.0))
    check("(x+1)(x-1)", near(ExpressionParser.compile("(x+1)(x-1)")(3.0), 8.0))
    check("x sin(x)", near(ExpressionParser.compile("x sin(x)")(1.0), kotlin.math.sin(1.0)))

    // 函数
    check("sin(pi/2)", near(ExpressionParser.compile("sin(pi/2)")(0.0), 1.0))
    check("ln(e)", near(ExpressionParser.compile("ln(e)")(0.0), 1.0))
    check("lg(100)", near(ExpressionParser.compile("lg(100)")(0.0), 2.0))
    check("log(2,8)=3", near(ExpressionParser.compile("log(2,8)")(0.0), 3.0))
    check("sqrt(16)", near(ExpressionParser.compile("sqrt(16)")(0.0), 4.0))
    check("exp(0)", near(ExpressionParser.compile("exp(0)")(0.0), 1.0))
    check("max(2,3)", near(ExpressionParser.compile("max(2,3)")(0.0), 3.0))
    check("floor(2.7)", near(ExpressionParser.compile("floor(2.7)")(0.0), 2.0))

    // 绝对值
    check("|x|", near(ExpressionParser.compile("|x|")(-5.0), 5.0))
    check("abs(-3)", near(ExpressionParser.compile("abs(-3)")(0.0), 3.0))

    // 容错
    check("f(x)= 前缀", near(ExpressionParser.compile("f(x) = x^2")(2.0), 4.0))
    check("全角括号", near(ExpressionParser.compile("（x＋1）".replace('＋', '+'))(2.0), 3.0))
    check("× ÷", near(ExpressionParser.compile("2×3÷2")(0.0), 3.0))

    // 错误检测
    check("空表达式报错", ExpressionParser.validate("") != null)
    check("未知函数报错", ExpressionParser.validate("foo(x)") != null)
    check("括号不匹配报错", ExpressionParser.validate("(x+1") != null)
    check("合法表达式通过", ExpressionParser.validate("sin(x)+x^2/4") == null)

    // 无定义区间
    val ln = ExpressionParser.compile("ln(x)")
    check("ln(-1) 为 NaN", ln(-1.0).isNaN())

    // 采样：ln(x) 在 x<=0 无定义，应从正区间开始才有段
    val segs = Sampler.sample(ln, -10.0, 10.0, 1000)
    check("ln 采样非空", segs.isNotEmpty())
    check("ln 采样点全部 x>0", segs.all { s -> s.xs.all { it > 0f } })

    // 渐近线：tan(x) 在 pi/2 附近断开，不应有跨越极点的巨长连线段
    val tan = ExpressionParser.compile("tan(x)")
    val tanSegs = Sampler.sample(tan, -3.0, 3.0, 2000)
    check("tan 被分成多段", tanSegs.size >= 2)
    check("tan 段内无跨极点（每段 y 范围有限）", tanSegs.all { s ->
        var lo = Float.MAX_VALUE; var hi = -Float.MAX_VALUE
        for (y in s.ys) { if (y < lo) lo = y; if (y > hi) hi = y }
        hi - lo < 5000f
    })

    // 1/x：x=0 两侧分段
    val invSegs = Sampler.sample(ExpressionParser.compile("1/x"), -1.0, 1.0, 1000)
    check("1/x 分成两段", invSegs.size == 2)

    // 连续函数不该被误断：x^2
    val quadSegs = Sampler.sample(ExpressionParser.compile("x^2"), -10.0, 10.0, 500)
    check("x^2 只有一段", quadSegs.size == 1)

    // 网格步长
    check("niceStep(0.03)=0.05", near(Sampler.niceStep(0.03), 0.05, 1e-12))
    check("niceStep(0.7)=1", near(Sampler.niceStep(0.7), 1.0, 1e-12))
    check("niceStep(13)=20", near(Sampler.niceStep(13.0), 20.0, 1e-9))

    println("通过 $passed 项，失败 $failed 项")
    if (failed > 0) kotlin.system.exitProcess(1)
}
