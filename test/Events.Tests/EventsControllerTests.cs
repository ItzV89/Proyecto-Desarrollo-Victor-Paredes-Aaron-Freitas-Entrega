using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Events.Api.Infrastructure.Persistence;
using Events.Api.Controllers;
using Events.Api.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace Events.Tests
{
    public class EventsControllerTests
    {
        private EventsDbContext BuildInMemoryDb()
        {
            var opts = new DbContextOptionsBuilder<EventsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            var db = new EventsDbContext(opts);
            db.Database.EnsureCreated();
            return db;
        }

        [Fact]
        public async Task Confirm_ReturnsBadRequest_WhenPayloadNull()
        {
            var db = BuildInMemoryDb();
            var eventBus = new Mock<Events.Api.Infrastructure.Services.IEventBus>();
            var hub = new Mock<IHubContext<Events.Api.Hubs.SeatHub>>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ReservationsController>>();

            var controller = new ReservationsController(db, eventBus.Object, hub.Object, logger.Object);
            var result = await controller.Confirm(null);
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Confirm_ReturnsConflict_WhenSeatsNotLocked()
        {
            var db = BuildInMemoryDb();
            // seed a seat without lock
            var seat = new Seat { Id = Guid.NewGuid(), Code = "A1", IsAvailable = true, ScenarioId = Guid.NewGuid(), Price = 10.0M };
            db.Seats.Add(seat);
            await db.SaveChangesAsync();

            var eventBus = new Mock<Events.Api.Infrastructure.Services.IEventBus>();
            var hub = new Mock<IHubContext<Events.Api.Hubs.SeatHub>>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ReservationsController>>();

            var controller = new ReservationsController(db, eventBus.Object, hub.Object, logger.Object);

            var payload = new ReservationsController.ConfirmReservationRequest(Guid.NewGuid(), Guid.NewGuid(), new System.Collections.Generic.List<ReservationsController.SeatRef> { new ReservationsController.SeatRef(seat.ScenarioId, seat.Id) });
            var result = await controller.Confirm(payload);
            Assert.IsType<ConflictObjectResult>(result);
        }
    }
}
