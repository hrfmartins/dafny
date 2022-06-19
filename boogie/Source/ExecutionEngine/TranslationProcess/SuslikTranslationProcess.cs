using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Boogie;

public class SuslikTranslationProcess : ITranslationProcess {
  public string Translate(List<Counterexample> errors, string programName) {
    // Firstly, get the variables to be used in the headers (used as locations)
    var translated = "void " + programName + " (";
    var headerVars = findHeaderVars(errors);

    for (int i = 0; i < headerVars.Count - 1; i++) {
      translated += "loc " + headerVars[i].ToString().Split('#')[0] + ", ";
    }

    translated += "loc " + headerVars[headerVars.Count - 1].ToString().Split('#')[0] + ")\n\t";

    // _____________ pre-condition generation _____________
    var preCondGeneratedEntry = new List<Expr>();
    var preCalculatedState = new List<Expr>();
    CalculateState(errors, preCondGeneratedEntry, preCalculatedState);
    var failingEnsures = ((Ensures)((ReturnCounterexample)errors[0]).FailingEnsures).Condition;
    var generatedPreCond = genPreCond(preCalculatedState);
    var generatedPostCond = genPostCond(failingEnsures, preCondGeneratedEntry);

    return translated + generatedPreCond + generatedPostCond + "{??}";
  }

  public string PrintState(List<Counterexample> errors) {
    // Used to write to terminal tw.WriteLine("HELLO USER");
    string translation = "===============================\n";

    var state = new List<Expr>();

    foreach (var var in errors[0].Trace) {
      for (int i = 0; i < var.cmds.Count; i++) {
        var cmd = var.cmds[i];

        if (cmd.GetType() == typeof(AssertEnsuresCmd) || cmd.GetType() == typeof(AssertCmd)) {
        } else {
          // if (((AssumeCmd) cmd).Expr.Type)
          if ((((AssumeCmd)cmd).Expr).GetType() != typeof(LiteralExpr)) {
            var args = ((NAryExpr)((((AssumeCmd)cmd).Expr))).Args;
            var b = ((NAryExpr)(((AssumeCmd)cmd).Expr)).Fun;
            if (args[0].GetType() == typeof(IdentifierExpr)) {
              //state.Add(new Tuple<NAryExpr, NAryExpr>(a[0], a[1]));
              if (((IdentifierExpr)args[0]).Name != "$_Frame@0") {
                state.Add(((AssumeCmd)cmd).Expr);
                translation += mapAndPrint(((AssumeCmd)cmd).Expr) + "\n";
              }
            }
          }
        }
      }
    }

    return translation + "======================================\n";
  }


  public List<Expr> CalculateState(List<Counterexample> errors, List<Expr> preCondGenerated, List<Expr> state) {
    // Used to write to terminal tw.WriteLine("HELLO USER");

    foreach (var var in errors[0].Trace) {
      for (int i = 0; i < var.cmds.Count; i++) {
        var cmd = var.cmds[i];

        if (cmd.GetType() == typeof(AssertEnsuresCmd) || cmd.GetType() == typeof(AssertCmd)) {
        } else {
          // if (((AssumeCmd) cmd).Expr.Type)
          if ((((AssumeCmd)cmd).Expr).GetType() != typeof(LiteralExpr)) {
            var args = ((NAryExpr)((((AssumeCmd)cmd).Expr))).Args;
            if (args[0].GetType() == typeof(IdentifierExpr)) {
              //state.Add(new Tuple<NAryExpr, NAryExpr>(a[0], a[1]));
              if (((IdentifierExpr)args[0]).Name != "$_Frame@0") {
                if (var.Label == "PreconditionGeneratedEntry") {
                  preCondGenerated.Add(((AssumeCmd)cmd).Expr);
                }
                state.Add(((AssumeCmd)cmd).Expr);
              }
            }
          }
        }
      }
    }

    return state;
  }

  public string mapAndPrint(Expr hel) {
    if (hel.GetType() == typeof(LiteralExpr)) {
      return (((LiteralExpr)hel).ToString()).Split('#')[0];
    } else if (hel.GetType() == typeof(IdentifierExpr)) {
      return (((IdentifierExpr)hel).Name).Split('#')[0];
    } else if (hel.GetType() == typeof(NAryExpr)) {
      if (((NAryExpr)hel).Args.Count == 2) {
        return mapAndPrint(((NAryExpr)hel).Args[0]) + ((NAryExpr)hel).Fun.FunctionName +
               mapAndPrint(((NAryExpr)hel).Args[1]);
      } else if (((NAryExpr)hel).Args.Count == 1) {
        return mapAndPrint(((NAryExpr)hel).Args[0]);
      }
    }

    return "";
  }

  public List<IdentifierExpr> findHeaderVars(List<Counterexample> errors) {
    List<IdentifierExpr> filtered = new List<IdentifierExpr>();
    foreach (var var in errors[0].Trace) {
      for (int i = 0; i < var.cmds.Count; i++) {
        var cmd = var.cmds[i];
        if (cmd.GetType() == typeof(AssertEnsuresCmd) || cmd.GetType() == typeof(AssertCmd)) {
        } else {
          if ((((AssumeCmd)cmd).Expr).GetType() != typeof(LiteralExpr)) {
            var args = ((NAryExpr)((((AssumeCmd)cmd).Expr))).Args;
            var b = ((NAryExpr)(((AssumeCmd)cmd).Expr)).Fun;
            if (args[0].GetType() == typeof(IdentifierExpr)) {
              //state.Add(new Tuple<NAryExpr, NAryExpr>(a[0], a[1]));
              if (((IdentifierExpr)args[0]).Name != "$_Frame@0") {
                filtered.Add((IdentifierExpr)args[0]);
              }
            }
          }
        }
      }
    }

    return filtered;
  }

  public string genPreCond(List<Expr> exprs) {
    var translation = "{ ";
    var usedMappings = new List<Dictionary<string, string>>();
    int i = 0;
    for (i = 0; i < exprs.Count - 1; i++) {
      var expr = exprs[i];

      translation += ((IdentifierExpr)((NAryExpr)expr).Args[0]).Name.Split("#")[0] + ":-> " +
                     generateMapping(usedMappings, ((NAryExpr)expr).Args[1])
                     + " ** ";
    }

    translation += ((IdentifierExpr)((NAryExpr)exprs[i]).Args[0]).Name.Split("#")[0] + " :-> " +
                   generateMapping(usedMappings, ((NAryExpr)exprs[i]).Args[1]);

    return translation + "}";
  }

  public string genPreEntryCond(List<Expr> preCondVariables) {
    var translation = " {";

    foreach (var preCond in preCondVariables) {
      translation += ((IdentifierExpr)((NAryExpr)preCond).Args[0]).Name.Split("#")[0] + " :-> " +
                     mapAndPrint(((NAryExpr)preCond).Args[1]);
    }

    return translation;
  }

  public string genPostCond(Expr expr, List<Expr> preCondVariables) {
    var translation = genPreEntryCond(preCondVariables);

    translation += " ** " + ((IdentifierExpr)((NAryExpr)expr).Args[0]).Name.Split("#")[0] + " :-> " +
                   mapAndPrint(((NAryExpr)expr).Args[1]);

    return translation + "}\n";
  }


  private string generateMapping(List<Dictionary<string, string>> mappings, Expr hel) {
    // The right side of the assignment
    // If it's a literal, it stays as-is
    // If it's a IdentifierExpr, we need to check if there's a mapping for it, otherwise we generate one

    if (hel.GetType() == typeof(LiteralExpr)) {
      var st = (((LiteralExpr)hel).ToString()).Split('#')[0];
      return st;
    } else if (hel.GetType() == typeof(IdentifierExpr)) {
      // CHECK FOR MAPPINGS OR CREATE
      return (((IdentifierExpr)hel).Name).Split('#')[0];
    } else if (hel.GetType() == typeof(NAryExpr)) {
      if (((NAryExpr)hel).Args.Count == 2) {
        return generateMapping(mappings, ((NAryExpr)hel).Args[0]) + ((NAryExpr)hel).Fun.FunctionName +
               generateMapping(mappings, ((NAryExpr)hel).Args[1]);
      } else if (((NAryExpr)hel).Args.Count == 1) {
        return generateMapping(mappings, ((NAryExpr)hel).Args[0]);
      }
    }

    return "";
  }
}