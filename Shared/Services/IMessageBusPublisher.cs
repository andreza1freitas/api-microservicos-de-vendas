using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.Events;

namespace Vendas.API.Services
{
    public interface IMessageBusPublisher
    {
        void PublishEvent(PedidoCriadoEvent evento);
    }
}