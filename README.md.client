[![Build Status](https://ci.appveyor.com/api/projects/status/github/MultiWorldTesting/decision?branch=master&svg=true)](https://ci.appveyor.com/project/lhoang29/decision)

Client Library
========

The Client Library is the user-facing component of the Decision Service which encompasses the "exploration" part (see [Exploration Library](https://github.com/microsoft?utf8=%E2%9C%93&query=mwt-ds-explore)) and logs gathered data via communications with its Azure service counterpart. It also has the ability to keep track of trained models and automatically updating to the latest model. For more information, see our [MWT Home Page](http://aka.ms/mwt).

The library is most conveniently used as part of a Decision Service deployment, where all configurations are specified. In this case, users simply need to pass in a URL of the application settings. See the [sample code repository](https://github.com/Microsoft/mwt-ds-decision/tree/master/ClientDecisionServiceSample) for more complete code:

```
  var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri);
  using (var service = DecisionService.Create<MyContext>(serviceConfig))
  {
    . . .
    var action = service.ChooseAction(uniqueKey, context, . . .);
  }
```

The Decision Service trains new models via [Vowpal Wabbit](https://github.com/JohnLangford/vowpal_wabbit) and thus in order to perform predictions client-side, the Client Library makes use of the same library. In this release, exploration algorithms are embedded into these models and can be controlled via [VW flags](https://github.com/JohnLangford/vowpal_wabbit/wiki/Contextual-Bandit-algorithms). While VW provides advanced algorithms, the client may be in cold-start state when no model has been deployed. In this case, CL supports simple Epsilon Greedy exploration (see the previous link for more details). Applications which cannot afford random decisions may provide a "default policy" and reduces epsilon to gain a more deterministic behavior. This can be achieved by first implementing the `IPolicy` interface and passing it to the `DecisionService` object:

```
  class MyPolicy : IPolicy<MyContext>
  {
    PolicyDecision<int> MapContext(MyContext context)
    {
      int chosenAction = [determine an action based on given context];
      return PolicyDecision.Create(chosenAction);
    }
  }
  
  . . .
  var myPolicy = new MyPolicy();
  int chosenAction = service.ChooseAction(key, context, myPolicy);
```

Build Prerequisites: Visual Studio 2013 or Visual Studio 2015.

