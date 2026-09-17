# Distributed System Tests for Promethei node

## Contributing tests
Do you want to write some tests using this distributed test setup? Great! Here's what you do.

1. Create a branch. Name it something descriptive, but start it with `tests/` please. [Example: `tests/data-redundancy`.]
1. Checkout your branch.
1. Create a new assembly in the `/Tests` folder. This can be an NUnit test assembly or simply a console app.
1. Add Project references to `Core`, as well as any project plugin you'll be using.
1. Write tests! Use existing tests for inspiration.

## Tips for writing tests for Promethei
### Transient tests
1. Add new code files to `Tests/PrometheiTests`
1. Inherrit from `PrometheiDistTest` or `AutoBootstrapDistTest`.
1. When using `PrometheiDistTest`:
   1. You must start your own Promethei bootstrap node. You can use `AddPromethei(...)` for this.
   1. When you start other Promethei nodes with `AddPromethei(...)` you can pass the bootstrap node by adding the `.WithBootstrapNode(...)` option.
1. When using `AutoBootstrapDistTest`:
   1. The test-infra creates the bootstrap node for you, and automatically passes it to each Promethei node you create in your tests. Handy for keeping your tests clean and to-the-point.
   1. When using the auto-bootstrap, you have no control over the bootstrap node from your tests. You can't (for example) shut it down during the course of the test. If you need this level of control for your scenario, use the `PrometheiDistTest` instead.
1. If your test needs a long time to run, add the `[UseLongTimeouts]` function attribute. This will greatly increase maximum time-out values for operations like for example uploading and downloading files.
### Continuous tests
1. Add new code files to `Tests/PrometheiContinuousTests/Tests`
1. Inherrit from `ContinuousTest`
1. Define one or more methods and decorate them with the `[TestMoment(...)]` attribute.
1. The TestMoment takes a number of seconds as argument. Each moment will be executed by the continuous test runner applying the given seconds as delay. (Non-cumulative. So two moments at T:10 will be executed one after another without delay, in this case the order of execution should not be depended upon.)
1. Continuous tests automatically receive access to the Promethei nodes that the tests are being run against.
1. Additionally, Continuous tests can start their own transient Promethei nodes and bootstrap them against the persistent nodes.

### Tips for either type of test
1. You can generate files of random test data by calling `GenerateTestFile(...)`.
1. You can enable access to the Promethei node metrics by adding the option `.EnableMetrics()`. Enabling metrics will make the test-infra download and save all Promethei metrics in case of a test failure. (The metrics are stored as CSV, in the same location as the test log file.)
1. You can enable access to the blockchain marketplace by adding the option `.EnableMarketplace(...)`.
1. Enabling metrics and/or enabling the marketplace takes extra resources from the test-infra and increases the time needed during Promethei node setup. Please don't enable these features unless your tests need them.
1. Tip: Promethei nodes can be named. Use the option `WithName(...)` and make reading your test logs a little nicer!
1. Tip: Commit often.

## Don't forget
1. Once you're happy with your tests, please create a pull-request and ask a Promethei core contributor to review your changes.
