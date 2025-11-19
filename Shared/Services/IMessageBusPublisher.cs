using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Events;

namespace Shared.Services
{
    public interface IMessageBusPublisher
    {
        void PublishEvent(PedidoCriadoEvent evento);
    }
}