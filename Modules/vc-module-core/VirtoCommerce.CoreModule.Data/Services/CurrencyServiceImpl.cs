using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CoreModule.Core.Model;
using VirtoCommerce.CoreModule.Core.Services;
using VirtoCommerce.CoreModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;

namespace VirtoCommerce.CoreModule.Data.Services
{
    public class CurrencyServiceImpl : ICurrencyService
    {
        private readonly Func<ICommerceRepository> _repositoryFactory;
        private readonly IEventPublisher _eventPublisher;

        public CurrencyServiceImpl(Func<ICommerceRepository> repositoryFactory, IEventPublisher eventPublisher)
        {
            _repositoryFactory = repositoryFactory;
            _eventPublisher = eventPublisher;
        }

        public async Task<IEnumerable<Currency>> GetAllCurrenciesAsync()
        {
            using (var repository = _repositoryFactory())
            {
                var currencyEntities = await repository.Currencies.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Code).ToArrayAsync();
                var result = currencyEntities.Select(x => x.ToModel(AbstractTypeFactory<Currency>.TryCreateInstance())).ToList();

                return result;
            }
        }

        public async Task UpsertCurrenciesAsync(Currency[] currencies)
        {
            if (currencies == null)
            {
                throw new ArgumentNullException(nameof(currencies));
            }

            var pkMap = new PrimaryKeyResolvingMap();
            using (var repository = _repositoryFactory())
            {
                //Ensure that only one Primary currency
                if (currencies.Any(x => x.IsPrimary))
                {
                    var oldPrimaryCurrency = await repository.Currencies.FirstOrDefaultAsync(x => x.IsPrimary);

                    if (oldPrimaryCurrency != null)
                    {
                        oldPrimaryCurrency.IsPrimary = false;
                    }
                }

                foreach (var currency in currencies)
                {
                    var sourceEntry = AbstractTypeFactory<Model.CurrencyEntity>.TryCreateInstance().FromModel(currency);
                    var targetEntry = await repository.Currencies.FirstOrDefaultAsync(x => x.Code == currency.Code);

                    if (targetEntry == null)
                    {
                        repository.Add(sourceEntry);
                    }
                    else
                    {
                        sourceEntry.Patch(targetEntry);
                    }
                }

                await repository.UnitOfWork.CommitAsync();
            }
        }

        public async Task DeleteCurrenciesAsync(string[] codes)
        {
            using (var repository = _repositoryFactory())
            {
                var currencyEntities = await repository.Currencies.Where(x => codes.Contains(x.Code)).ToArrayAsync();
                foreach (var currency in currencyEntities)
                {
                    if (currency.IsPrimary)
                    {
                        throw new ArgumentException("Unable to delete primary currency");
                    }

                    repository.Remove(currency);
                }

                await repository.UnitOfWork.CommitAsync();
            }
        }
    }
}
