using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;

namespace VC
{
  public class CheckerPool
  {
    private readonly ConcurrentStack<Checker> availableCheckers = new();
    private readonly ConcurrentQueue<TaskCompletionSource<Checker>> checkerWaiters = new();
    private int notCreatedCheckers;
    private bool disposed;

    public VCGenOptions Options { get; }

    public CheckerPool(VCGenOptions options)
    {
      this.Options = options;
      notCreatedCheckers = options.VcsCores;
    }

    public Task<Checker> FindCheckerFor(ConditionGeneration vcgen, Split split = null)
    {
      if (disposed) {
        return Task.FromException<Checker>(new Exception("CheckerPool was already disposed"));
      }
        
      if (availableCheckers.TryPop(out var result)) {
        PrepareChecker(vcgen.program, split, result);
        Contract.Assert(result != null);
        return Task.FromResult(result);
      }

      var afterDecrement = Interlocked.Decrement(ref notCreatedCheckers);
      if (afterDecrement >= 0) {
        var checker = CreateNewChecker();
        PrepareChecker(vcgen.program, split, checker);
        Contract.Assert(checker != null);
        return Task.FromResult(checker);
      }
      Interlocked.Increment(ref notCreatedCheckers);

      var source = new TaskCompletionSource<Checker>();
      checkerWaiters.Enqueue(source);
      return source.Task.ContinueWith(t =>
      {
        PrepareChecker(vcgen.program, split, t.Result);
        Contract.Assert(t.Result != null);
        return t.Result;
      });
    }

    private Checker CreateNewChecker()
    {
      var log = Options.ProverLogFilePath;
      if (log != null && !log.Contains("@PROC@") && availableCheckers.Count > 0) {
        log = log + "." + availableCheckers.Count;
      } 

      return new Checker(this, log, Options.ProverLogFileAppend);
    }

    public void Dispose()
    {
      while (availableCheckers.TryPop(out var checker)) {
        checker.Close();
      }
      disposed = true;
    }

    void PrepareChecker(Program program, Split split, Checker checker)
    {
      if (checker.WillingToHandle(program) && (split == null || checker.SolverOptions.RandomSeed == split.RandomSeed && !Options.Prune))
      {
        checker.GetReady();
        return;
      }

      checker.Target(program, checker.TheoremProver.Context, split);
      checker.GetReady();
    }

    public void AddChecker(Checker checker)
    {
      if (checker.IsClosed) {
        throw new Exception();
      }
      if (disposed) {
        checker.Close();
        return;
      }
      if (checkerWaiters.TryDequeue(out var waiter)) {
        if (waiter.TrySetResult(checker)) {
          return;
        }
      }

      availableCheckers.Push(checker);
    }

    public void CheckerDied()
    {
      if (checkerWaiters.TryDequeue(out var waiter)) {
        waiter.SetResult(CreateNewChecker());
      } else {
        Interlocked.Increment(ref notCreatedCheckers);
      }
    }
  }
}