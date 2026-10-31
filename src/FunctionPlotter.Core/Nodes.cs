using System;

namespace FunctionPlotter.Core
{
    /// <summary>表达式语法树节点。求值时只传入自变量 x。</summary>
    internal abstract class Node
    {
        public abstract double Eval(double x);
    }

    internal sealed class ConstantNode : Node
    {
        private readonly double _value;

        public ConstantNode(double value) => _value = value;

        public override double Eval(double x) => _value;
    }

    internal sealed class VariableNode : Node
    {
        public static readonly VariableNode Instance = new VariableNode();

        private VariableNode() { }

        public override double Eval(double x) => x;
    }

    internal sealed class NegateNode : Node
    {
        private readonly Node _child;

        public NegateNode(Node child) => _child = child;

        public override double Eval(double x) => -_child.Eval(x);
    }

    internal enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Power
    }

    internal sealed class BinaryNode : Node
    {
        private readonly BinaryOperator _op;
        private readonly Node _left;
        private readonly Node _right;

        public BinaryNode(BinaryOperator op, Node left, Node right)
        {
            _op = op;
            _left = left;
            _right = right;
        }

        public override double Eval(double x)
        {
            double a = _left.Eval(x);
            double b = _right.Eval(x);
            switch (_op)
            {
                case BinaryOperator.Add: return a + b;
                case BinaryOperator.Subtract: return a - b;
                case BinaryOperator.Multiply: return a * b;
                case BinaryOperator.Divide: return a / b;
                case BinaryOperator.Power: return Math.Pow(a, b);
                default: return double.NaN;
            }
        }
    }

    internal sealed class UnaryCallNode : Node
    {
        private readonly Func<double, double> _function;
        private readonly Node _argument;

        public UnaryCallNode(Func<double, double> function, Node argument)
        {
            _function = function;
            _argument = argument;
        }

        public override double Eval(double x) => _function(_argument.Eval(x));
    }

    internal sealed class BinaryCallNode : Node
    {
        private readonly Func<double, double, double> _function;
        private readonly Node _first;
        private readonly Node _second;

        public BinaryCallNode(Func<double, double, double> function, Node first, Node second)
        {
            _function = function;
            _first = first;
            _second = second;
        }

        public override double Eval(double x) => _function(_first.Eval(x), _second.Eval(x));
    }
}
