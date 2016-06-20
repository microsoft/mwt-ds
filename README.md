[![Build Status](https://ci.appveyor.com/api/projects/status/github/MultiWorldTesting/decision?branch=master&svg=true)](https://ci.appveyor.com/project/lhoang29/decision)

Client Library
========

The Client Library is the user-facing component of the Decision Service which encompasses the "exploration" part (see [Exploration Library](https://github.com/microsoft?utf8=%E2%9C%93&query=mwt-ds-explore)) and logs gathered data via communications with its Azure service counterpart. It also has the ability to keep track of trained models and automatically updating to the latest model. For more information, see our [MWT Home Page](http://aka.ms/mwt).

The library is most conveniently used as part of a Decision Service deployment, where all configurations are specified. In this case, users simply need to pass in a URL of the application settings:

```
  var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri);
  using (var service = DecisionService.Create<MyContext>(serviceConfig))
  {
    . . .
    var action = service.ChooseAction(uniqueKey, context, . . .);
  }
```

See the [sample code repository](https://github.com/Microsoft/mwt-ds-decision/tree/master/ClientDecisionServiceSample) for more details.



