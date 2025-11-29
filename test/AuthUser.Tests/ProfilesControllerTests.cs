using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using AuthUser.Api.Controllers;
using AuthUser.Api.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AuthUser.Tests
{
    public class ProfilesControllerTests
    {
        [Fact]
        public async Task Create_ReturnsCreated_WhenMediatorReturnsId()
        {
            var mediator = new Mock<IMediator>();
            var repo = new Mock<IProfileRepository>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ProfilesController>>();

            var generatedId = Guid.NewGuid();
            mediator.Setup(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>())).ReturnsAsync(generatedId);
            repo.Setup(r => r.GetByIdAsync(generatedId)).ReturnsAsync(new AuthUser.Api.Domain.Entities.Profile { Id = generatedId, Username = "u" });

            var controller = new ProfilesController(mediator.Object, repo.Object, logger.Object);

            var cmd = new AuthUser.Api.Application.Commands.CreateProfileCommand("u","u@x.com", null, null);
            var result = await controller.Create(cmd, CancellationToken.None);

            Assert.IsType<CreatedAtActionResult>(result);
        }
    }
}
