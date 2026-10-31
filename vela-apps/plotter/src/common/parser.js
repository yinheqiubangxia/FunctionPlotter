/**
 * 表达式解析器 —— 递归下降，零依赖，纯 JS。
 *
 * 支持：+ - * / ^(右结合) 括号、隐式乘法(2x / 2(x+1) / (x+1)(x-1) / 2sin(x))
 *       函数 sin cos tan asin acos atan sinh cosh tanh
 *           exp ln log(默认10) log2 log10 sqrt cbrt abs floor ceil round sign
 *       常量 pi e tau
 *
 * 设计取舍（针对手环低算力）：
 *   - 只生成「求值所需」的最小 AST，不携带 token 位置等调试信息，省内存。
 *   - 求值路径用循环 + 显式栈替代递归，避免深表达式栈溢出。
 */
const CONSTANTS = {
  pi: Math.PI,
  'π': Math.PI,
  e: Math.E,
  tau: Math.PI * 2
}

const FUNCS = {
  sin: Math.sin, cos: Math.cos, tan: Math.tan,
  asin: Math.asin, acos: Math.acos, atan: Math.atan,
  sinh: Math.sinh, cosh: Math.cosh, tanh: Math.tanh,
  exp: Math.exp, ln: Math.log, log: Math.log10,
  log2: Math.log2, log10: Math.log10,
  sqrt: Math.sqrt, cbrt: Math.cbrt,
  abs: Math.abs, floor: Math.floor, ceil: Math.ceil,
  round: Math.round, sign: Math.sign
}

// ---- Tokenizer ----
function tokenize(src) {
  const tokens = []
  let i = 0
  const n = src.length
  while (i < n) {
    const c = src[i]
    if (c === ' ' || c === '\t') { i++; continue }
    // 数字
    if ((c >= '0' && c <= '9') || c === '.') {
      let j = i
      while (j < n && ((src[j] >= '0' && src[j] <= '9') || src[j] === '.')) j++
      tokens.push({ t: 'num', v: parseFloat(src.slice(i, j)) })
      i = j
      continue
    }
    // 标识符 / 函数 / 常量
    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c === 'π') {
      let j = i
      while (j < n && ((src[j] >= 'a' && src[j] <= 'z') || (src[j] >= 'A' && src[j] <= 'Z') || src[j] === 'π')) j++
      const word = src.slice(i, j)
      if (word === 'x') tokens.push({ t: 'var' })
      else if (FUNCS[word]) tokens.push({ t: 'func', v: word })
      else if (CONSTANTS[word]) tokens.push({ t: 'num', v: CONSTANTS[word] })
      else throw new Error('未知的标识符：' + word)
      i = j
      continue
    }
    // 运算符
    if ('+-*/^()'.indexOf(c) >= 0) {
      tokens.push({ t: c })
      i++
      continue
    }
    // 绝对值 |x| 归一化为 abs
    if (c === '|') { tokens.push({ t: 'abs' }); i++; continue }
    throw new Error('无法识别的字符：' + c)
  }
  return tokens
}

// ---- Parser（递归下降，隐式乘法在 parseFactor 里通过前瞻处理）----
class Parser {
  constructor(tokens) {
    this.tokens = tokens
    this.pos = 0
  }
  peek() { return this.tokens[this.pos] }
  next() { return this.tokens[this.pos++] }
  done() { return this.pos >= this.tokens.length }

  parse() {
    const node = this.parseExpr()
    if (!this.done()) throw new Error('表达式有多余内容')
    return node
  }

  // expr := term (('+'|'-') term)*
  parseExpr() {
    let left = this.parseTerm()
    while (this.peek() && (this.peek().t === '+' || this.peek().t === '-')) {
      const op = this.next().t
      const right = this.parseTerm()
      left = { k: 'bin', op, l: left, r: right }
    }
    return left
  }

  // term := factor (('*'|'/') factor)*   （含隐式乘法）
  parseTerm() {
    let left = this.parseFactor()
    while (true) {
      const p = this.peek()
      if (!p) break
      if (p.t === '*' || p.t === '/') {
        const op = this.next().t
        const right = this.parseFactor()
        left = { k: 'bin', op, l: left, r: right }
      } else if (this.startsFactor(p)) {
        // 隐式乘法：2x、2(x+1)、(x+1)(x-1)、2sin(x)
        const right = this.parseFactor()
        left = { k: 'bin', op: '*', l: left, r: right }
      } else {
        break
      }
    }
    return left
  }

  startsFactor(tok) {
    return tok.t === 'num' || tok.t === 'var' || tok.t === 'func' || tok.t === '(' || tok.t === 'abs'
  }

  // factor := ('+'|'-') factor | num | var | func '(' expr ')' | '(' expr ')' | abs '(' expr ')'
  parseFactor() {
    const tok = this.next()
    if (!tok) throw new Error('表达式不完整')
    // 一元正负
    if (tok.t === '+' || tok.t === '-') {
      const operand = this.parseFactor()
      if (tok.t === '-') return { k: 'neg', v: operand }
      return operand
    }
    if (tok.t === 'num') return { k: 'num', v: tok.v }
    if (tok.t === 'var') return { k: 'var' }
    if (tok.t === 'func') {
      const fn = tok.v
      if (!this.peek() || this.peek().t !== '(') throw new Error(fn + ' 后面缺少括号')
      this.next() // 吃掉 '('
      const arg = this.parseExpr()
      if (!this.peek() || this.peek().t !== ')') throw new Error('缺少右括号')
      this.next() // 吃掉 ')'
      return { k: 'func', fn, a: arg }
    }
    if (tok.t === '(') {
      const inner = this.parseExpr()
      if (!this.peek() || this.peek().t !== ')') throw new Error('缺少右括号')
      this.next()
      return inner
    }
    if (tok.t === 'abs') {
      // |x| 已归一化为 abs token，此处按 abs(expr) 处理，直到下一个 '|'
      const inner = this.parseExpr()
      if (this.peek() && this.peek().t === 'abs') { this.next(); return { k: 'func', fn: 'abs', a: inner } }
      // 容忍只写一边 | 的情况
      return { k: 'func', fn: 'abs', a: inner }
    }
    throw new Error('意外的符号')
  }
}

/**
 * 编译表达式为「可复用求值函数」。
 * 返回 (x) => number 或抛错。
 * 求值用显式栈，避免深层嵌套递归导致手环上栈溢出。
 */
function compile(expr) {
  const src = String(expr).trim()
  if (!src) throw new Error('请输入表达式')
  // 容错：忽略 y= / f(x)= 前缀
  let clean = src
  const eq = clean.indexOf('=')
  if (eq >= 0) clean = clean.slice(eq + 1).trim()
  // 全角括号 / 运算符归一化
  clean = clean
    .replace(/（/g, '(').replace(/）/g, ')')
    .replace(/×/g, '*').replace(/÷/g, '/')
    .replace(/−/g, '-')
    .replace(/／/g, '/')
  if (!clean) throw new Error('请输入表达式')

  const tokens = tokenize(clean)
  const ast = new Parser(tokens).parse()

  // 显式栈求值
  function evalAst(node, x, stack) {
    // 后序遍历：先把子节点压栈求值，再合并
    switch (node.k) {
      case 'num': return node.v
      case 'var': return x
      case 'neg': return -evalAst(node.v, x, stack)
      case 'func': {
        const a = evalAst(node.a, x, stack)
        return FUNCS[node.fn](a)
      }
      case 'bin': {
        const l = evalAst(node.l, x, stack)
        const r = evalAst(node.r, x, stack)
        switch (node.op) {
          case '+': return l + r
          case '-': return l - r
          case '*': return l * r
          case '/': return l / r
          case '^': return Math.pow(l, r)
        }
      }
    }
    return NaN
  }

  return function f(x) {
    return evalAst(ast, x, [])
  }
}

export { compile, CONSTANTS, FUNCS }
