using System;
using System.Collections.Generic;

namespace Microsoft.Boogie;

public class TranslationFactory {
  public ITranslationProcess GetTranslation(string type) {
    switch (type) {
      case "suslik": return new SuslikTranslationProcess();
      default: throw new ArgumentException("Invalid type", type);
    }
  }
}