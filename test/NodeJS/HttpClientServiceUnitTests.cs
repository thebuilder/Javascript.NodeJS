﻿using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using Xunit;

namespace Jering.Javascript.NodeJS.Tests
{
    public class HttpClientServiceUnitTests
    {
        private readonly MockRepository _mockRepository = new MockRepository(MockBehavior.Default);

        [Theory]
        [MemberData(nameof(Constructor_SetsTimeout_Data))]
        public void Constructor_SetsTimeout(int dummyTimeoutMS, TimeSpan expectedTimeoutMS)
        {
            // Arrange
            var dummyOptions = new OutOfProcessNodeJSServiceOptions() { TimeoutMS = dummyTimeoutMS };
            Mock<IOptions<OutOfProcessNodeJSServiceOptions>> mockOptionsAccessor = _mockRepository.Create<IOptions<OutOfProcessNodeJSServiceOptions>>();
            mockOptionsAccessor.Setup(o => o.Value).Returns(dummyOptions);
            using var dummyHttpClient = new HttpClient();

            // Act
            var result = new HttpClientService(dummyHttpClient, mockOptionsAccessor.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedTimeoutMS, result.Timeout);
        }

        public static IEnumerable<object[]> Constructor_SetsTimeout_Data()
        {
            return new object[][]
            {
                // -1 == infinite
                new object[]{ -1, Timeout.InfiniteTimeSpan},
                // All other values == value + 1000
                new object[]{ 0, TimeSpan.FromMilliseconds(1000)},
                new object[]{ 1000, TimeSpan.FromMilliseconds(2000)}
            };
        }
    }
}
