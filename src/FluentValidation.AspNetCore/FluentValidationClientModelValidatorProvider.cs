﻿namespace FluentValidation.AspNetCore {
	using System;
	using System.Collections.Generic;
	using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
	using System.Linq;
	using System.Reflection;
	using FluentValidation.Internal;
	using FluentValidation.Validators;

	public delegate IClientModelValidator FluentValidationClientValidatorFactory(ClientValidatorProviderContext context, PropertyRule rule, IPropertyValidator validator);

	public class FluentValidationClientModelValidatorProvider : IClientModelValidatorProvider{

		public Dictionary<Type, FluentValidationClientValidatorFactory> ClientValidatorFactories => _validatorFactories;

		private readonly Dictionary<Type, FluentValidationClientValidatorFactory> _validatorFactories = new Dictionary<Type, FluentValidationClientValidatorFactory>() {
			{ typeof(INotNullValidator), (context, rule, validator) => new RequiredClientValidator(rule, validator) },
			{ typeof(INotEmptyValidator), (context, rule, validator) => new RequiredClientValidator(rule, validator) },
			// email must come before regex.
			{ typeof(IEmailValidator), (context, rule, validator) => new EmailClientValidator(rule, validator) },
			{ typeof(IRegularExpressionValidator), (context, rule, validator) => new RegexClientValidator(rule, validator) },
			{ typeof(ILengthValidator), (context, rule, validator) => new StringLengthClientValidator(rule, validator)},
			{ typeof(InclusiveBetweenValidator), (context, rule, validator) => new RangeClientValidator(rule, validator) },
			{ typeof(GreaterThanOrEqualValidator), (context, rule, validator) => new MinLengthClientValidator(rule, validator) },
			{ typeof(LessThanOrEqualValidator), (context, rule, validator) => new MaxLengthClientValidator(rule, validator) },
			{ typeof(EqualValidator), (context, rule, validator) => new EqualToClientValidator(rule, validator) },
			{ typeof(CreditCardValidator), (context, description, validator) => new CreditCardClientValidator(description, validator) }
		};

		IValidatorFactory _validatorFactory;

		public FluentValidationClientModelValidatorProvider(IValidatorFactory validatorFactory) {
			_validatorFactory = validatorFactory;
		}

		public void CreateValidators(ClientValidatorProviderContext context) {
			var modelType = context.ModelMetadata.ContainerType;

			if (modelType != null ) {
				var validator = _validatorFactory.GetValidator(modelType);
				var descriptor = validator.CreateDescriptor();
				var propertyName = context.ModelMetadata.PropertyName;

				var validatorsWithRules = from rule in descriptor.GetRulesForMember(propertyName)
										  let propertyRule = (PropertyRule)rule
										  let validators = rule.Validators
										  where validators.Any()
										  from propertyValidator in validators
										  let modelValidatorForProperty = GetModelValidator( context, propertyRule, propertyValidator)
										  where modelValidatorForProperty != null
										  select modelValidatorForProperty;

				foreach (var propVal in validatorsWithRules) {
					context.Results.Add(new ClientValidatorItem {
						Validator = propVal,
						IsReusable = false
					});
				}
			}
		}

		protected virtual IClientModelValidator GetModelValidator(ClientValidatorProviderContext context, PropertyRule rule, IPropertyValidator propertyValidator)
		{
			var type = propertyValidator.GetType();

			var factory = _validatorFactories
				.Where(x => x.Key.IsAssignableFrom(type))
				.Select(x => x.Value)
				.FirstOrDefault();

			return factory?.Invoke(context, rule, propertyValidator);
		}
	}

}