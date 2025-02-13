namespace FinancialAnalyticsProcessor.Application.Common
{
    using AutoMapper;
    using System;

    public class MappingException : Exception
    {
        public MappingException() { }

        public MappingException(string message) : base(message) { }

        public MappingException(string message, Exception innerException)
            : base(message, innerException) { }

        public static MappingException FromAutoMapperException(Exception ex)
        {
            return ex switch
            {
                AutoMapperMappingException mappingEx => new MappingException("Error occurred while mapping objects.", mappingEx),
                AutoMapperConfigurationException configEx => new MappingException("Error in AutoMapper configuration.", configEx),
                _ => new MappingException("An unknown mapping error occurred.", ex)
            };
        }
    }
}
