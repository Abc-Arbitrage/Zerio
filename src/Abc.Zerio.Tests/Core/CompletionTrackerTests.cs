using Abc.Zerio.Core;
using NUnit.Framework;

namespace Abc.Zerio.Tests.Core
{
    [TestFixture]
    public class CompletionTrackerTests
    {
        private CompletionTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new CompletionTracker();
        }

        [Test]
        public void empty_tracker_should_not_identify_value_as_completed([Range(0, 50)] long value)
        {
            Assert.IsFalse(_tracker.IsCompleted(value));
        }

        [Test]
        public void should_not_identify_uncompleted_value_as_completed([Range(50, 100)] long value)
        {
            for (var i = 0; i < 50; i++)
            {
                _tracker.MarkAsCompleted(i);
            }

            Assert.IsFalse(_tracker.IsCompleted(value));
        }
        
        [Test]
        public void should_identify_unsorted_completed_value_as_completed()
        {
            _tracker.MarkAsCompleted(0);
            _tracker.MarkAsCompleted(1);
            _tracker.MarkAsCompleted(2);
            _tracker.MarkAsCompleted(3);
            _tracker.MarkAsCompleted(5);

            Assert.IsTrue(_tracker.IsCompleted(5));
        }

        [Test]
        public void should_not_identify_unsorted_uncompleted_value_as_completed()
        {
            _tracker.MarkAsCompleted(0);
            _tracker.MarkAsCompleted(1);
            _tracker.MarkAsCompleted(2);
            _tracker.MarkAsCompleted(3);
            _tracker.MarkAsCompleted(5);

            Assert.IsFalse(_tracker.IsCompleted(4));
        }

        [Test]
        public void should_not_keep_unneeded_values_in_cache()
        {
            _tracker.MarkAsCompleted(0);
            _tracker.MarkAsCompleted(1);
            
            _tracker.MarkAsCompleted(3);
            _tracker.MarkAsCompleted(4);
            _tracker.MarkAsCompleted(5);
            
            _tracker.MarkAsCompleted(2);

            Assert.That(_tracker.CacheSize, Is.EqualTo(1));
        }
    }
}
