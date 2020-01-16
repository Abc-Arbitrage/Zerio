using Abc.Zerio.Core;
using NUnit.Framework;

namespace Abc.Zerio.Tests.Core
{
    [TestFixture]
    public class CompletionPollingWaitStrategyFactoryTests
    {
        [Test]
        public void should_create_BusySpinWaitStrategy()
        {
            // Arrange
            var waitStrategyType = CompletionPollingWaitStrategyType.BusySpinWaitStrategy;

            // Act
            var waitStrategy = CompletionPollingWaitStrategyFactory.Create(waitStrategyType);

            // Assert
            Assert.IsInstanceOf<BusySpinCompletionPollingWaitStrategy>(waitStrategy);
        }

        [Test]
        public void should_create_SpinWaitWaitStrategy()
        {
            // Arrange
            var waitStrategyType = CompletionPollingWaitStrategyType.SpinWaitWaitStrategy;

            // Act
            var waitStrategy = CompletionPollingWaitStrategyFactory.Create(waitStrategyType);

            // Assert
            Assert.IsInstanceOf<SpinWaitCompletionPollingWaitStrategy>(waitStrategy);
        }
    }
}
