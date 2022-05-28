using System.Collections.Generic;
using System.IO;

namespace Microsoft.Boogie; 

public interface ITranslationProcess {
  public string Translate(List<Counterexample> errors, string programName);

  public string PrintState(List<Counterexample> errors);


}

