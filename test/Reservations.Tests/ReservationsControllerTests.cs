using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Moq;
using RichardSzalay.MockHttp;
using Xunit;
using Reservations.Api.Controllers;
using Reservations.Api.Domain.Repositories;
using Reservations.Api.Domain.Entities;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Reservations.Tests
{
    public class ReservationsControllerTests
    {
        [Fact]
        public async Task Create_ReturnsConflict_WhenEventsLockFails()
        {
            // Arrange
            var jobs = new Mock<IBackgroundJobClient>();
            var repo = new Mock<IReservationRepository>();

            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Post, "*/api/events/*/seats/*/lock")
                .Respond(HttpStatusCode.Conflict);
            var client = mockHttp.ToHttpClient();
            client.BaseAddress = new System.Uri("http://localhost/");

            var httpFactory = new Mock<System.Net.Http.HttpClientFactory>();
            // IHttpClientFactory is an interface; use Func via Moq by casting to IHttpClientFactory not present - instead use custom factory
            var httpFactoryMock = new Mock<System.Net.Http.IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient("events")).Returns(client);

            var sp = new Mock<System.IServiceProvider>();

            var controller = new Reservations.Api.Controllers.ReservationsController(jobs.Object, repo.Object, httpFactoryMock.Object, sp.Object);

            // Act
            var result = await controller.Create(new Reservations.Api.Controllers.CreateReservationRequest(System.Guid.NewGuid(), System.Guid.NewGuid(), System.Guid.NewGuid()));

            // Assert
            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task Create_SchedulesExpiration_WhenLockSucceeds()
        {
            var jobs = new Mock<IBackgroundJobClient>();
            var repo = new Mock<IReservationRepository>();

            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Post, "*/api/events/*/seats/*/lock")
                .Respond(HttpStatusCode.OK, "application/json", "{}");
            var client = mockHttp.ToHttpClient();
            client.BaseAddress = new System.Uri("http://localhost/");

            var httpFactoryMock = new Mock<System.Net.Http.IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient("events")).Returns(client);

            var sp = new Mock<System.IServiceProvider>();

            var controller = new Reservations.Api.Controllers.ReservationsController(jobs.Object, repo.Object, httpFactoryMock.Object, sp.Object);

            var eventId = System.Guid.NewGuid();
            var scenarioId = System.Guid.NewGuid();
            var seatId = System.Guid.NewGuid();

            var result = await controller.Create(new Reservations.Api.Controllers.CreateReservationRequest(eventId, scenarioId, seatId));

            Assert.IsType<AcceptedResult>(result);
            jobs.Verify(j => j.Schedule(It.IsAny<System.Linq.Expressions.Expression<System.Action>>(), It.IsAny<System.TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task CancelReservation_UpdatesStatus_And_Notifies()
        {
            var jobs = new Mock<IBackgroundJobClient>();
            var repo = new Mock<IReservationRepository>();
            var reservation = new Reservation { Id = System.Guid.NewGuid(), EventId = System.Guid.NewGuid(), ScenarioId = System.Guid.NewGuid(), SeatId = System.Guid.NewGuid(), Status = "Pending" };
            repo.Setup(r => r.GetByIdAsync(reservation.Id)).ReturnsAsync(reservation);

            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Post, "*/api/reservations/notify").Respond(HttpStatusCode.OK);
            var client = mockHttp.ToHttpClient();
            client.BaseAddress = new System.Uri("http://localhost/");

            var httpFactoryMock = new Mock<System.Net.Http.IHttpClientFactory>();
            httpFactoryMock.Setup(f => f.CreateClient("events")).Returns(client);

            var sp = new Mock<System.IServiceProvider>();

            var controller = new Reservations.Api.Controllers.ReservationsController(jobs.Object, repo.Object, httpFactoryMock.Object, sp.Object);

            var res = await controller.CancelReservation(reservation.Id);

            Assert.IsType<StatusCodeResult>(res); // NoContent returns StatusCodeResult
            repo.Verify(r => r.UpdateAsync(It.Is<Reservation>(x => x.Status == "Cancelled")), Times.Once);
        }
    }
}
