using System;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics.Contracts;

namespace Microsoft.Boogie
{
  // public class FeatureMiner : StandardVisitor
  // {
  // }


  // TODO Fixing the missing modifies introduced by SMACK
  public class SMACKFix : ModifiedVariableCollector
  {
    public static void Scan(Program program)
    {
      SMACKFix sfix = new SMACKFix();
      sfix.Visit(program);
    }
    public override Implementation VisitImplementation(Implementation node)
    {
      Implementation impl = base.VisitImplementation(node);
      foreach(IdentifierExpr v  in _modifiedVars)
      {
        Console.WriteLine($"{node.Name} modifies {v.Name}");
        impl.Proc.Modifies.Add(v);
      }
      _modifiedVars.Clear();

      return impl;
    }
  }

  public class ModifiedVariableCollector : StandardVisitor
  {
    protected HashSet<Variable /*!*/> /*!*/
      _usedVars;

    protected HashSet<IdentifierExpr /*!*/> /*!*/
      _modifiedVars;

    public IEnumerable<Variable /*!*/> /*!*/ usedVars
    {
      get { return _usedVars.AsEnumerable(); }
    }

    protected HashSet<Variable /*!*/> /*!*/
      _oldVarsUsed;

    public IEnumerable<Variable /*!*/> /*!*/ oldVarsUsed
    {
      get { return _oldVarsUsed.AsEnumerable(); }
    }

    [ContractInvariantMethod]
    void ObjectInvariant()
    {
      Contract.Invariant(cce.NonNullElements(_usedVars));
      Contract.Invariant(cce.NonNullElements(_oldVarsUsed));
    }

    int insideOldExpr;

    public ModifiedVariableCollector()
    {
      _usedVars = new System.Collections.Generic.HashSet<Variable /*!*/>();
      _modifiedVars = new System.Collections.Generic.HashSet<IdentifierExpr /*!*/>();
      _oldVarsUsed = new System.Collections.Generic.HashSet<Variable /*!*/>();
      insideOldExpr = 0;
    }

    public override Expr VisitOldExpr(OldExpr node)
    {
      //Contract.Requires(node != null);
      Contract.Ensures(Contract.Result<Expr>() != null);
      insideOldExpr++;
      node.Expr = this.VisitExpr(node.Expr);
      insideOldExpr--;
      return node;
    }

    public override Expr VisitIdentifierExpr(IdentifierExpr node)
    {
      //Contract.Requires(node != null);
      Contract.Ensures(Contract.Result<Expr>() != null);
      if (node.Decl != null)
      {
        _usedVars.Add(node.Decl);
        if (insideOldExpr > 0)
        {
          _oldVarsUsed.Add(node.Decl);
        }
      }

      return node;
    }

    public override Cmd VisitAssignCmd(AssignCmd node)
    {
      foreach (AssignLhs lhs in node.Lhss)
      {
        _modifiedVars.Add(lhs.DeepAssignedIdentifier);
      }
      return base.VisitAssignCmd(node);
    }

    public static IEnumerable<Variable> Collect(Absy node)
    {
      var collector = new ModifiedVariableCollector();
      collector.Visit(node);
      return collector.usedVars;
    }

    public static IEnumerable<Variable> Collect(IEnumerable<Absy> nodes)
    {
      var collector = new ModifiedVariableCollector();
      foreach (var node in nodes)
        collector.Visit(node);
      return collector.usedVars;
    }
  }


  public class AssumeTrue : StandardVisitor
  {
    static Dictionary<Absy, Tuple<int,int>> d;
    public static void Scan(Program program, Dictionary<Absy, Tuple<int,int>> dict)
    {
      AssumeTrue detector = new AssumeTrue();
      d = dict;
      detector.Visit(program);
    }

    public override Cmd VisitAssumeCmd(AssumeCmd node)
    {
      if(node.Expr.Equals(Expr.True))
      {
        Console.WriteLine($"Found ASSUME true at L:{node.Line} C:{node.Col}");
        d.Add(node, Tuple.Create(node.Line, node.Col));
        // return new CommentCmd("");
      }
      return node;
    }
  }

  public class BuiltInAttr : StandardVisitor
  {
    static Dictionary<Absy, Tuple<int,int>> d;
    public static void Scan(Program program, Dictionary<Absy, Tuple<int,int>> dict)
    {
      BuiltInAttr detector = new BuiltInAttr();
      d = dict;
      detector.Visit(program);
    }

    private DeclWithFormals VisitDeclWithFormals_(DeclWithFormals node)
    {
      this.VisitVariableSeq(node.InParams);
      this.VisitVariableSeq(node.OutParams);

      if(QKeyValue.FindStringAttribute(node.Attributes, "builtin") != null)
      {
        Console.WriteLine($"Found :builtin function at {node.Name} L:{node.Attributes.Line} C:{node.Attributes.Col}");
        d.Add(node, Tuple.Create(node.Attributes.Line, node.Attributes.Col));
      }

      return node;
    }

    public override Function VisitFunction(Function node)
    {
      node = (Function) this.VisitDeclWithFormals_(node);
      if (node.Body != null)
      {
        node.Body = this.VisitExpr(node.Body);
      }
      else if (node.DefinitionBody != null){
        node.DefinitionBody = (NAryExpr) this.VisitExpr(node.DefinitionBody);
      }
      return node;
    }
  }

}
