using Atlas.Core.Entities.Tenant;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using AutoMapper;

namespace Atlas.Services.Mapping
{
    public class ProductProfile : Profile
    {
        public ProductProfile()
        {
            CreateMap<Store, StoreDto>().ReverseMap();

            // Product mappings
            CreateMap<Product, ProductDto>().ReverseMap();

            CreateMap<CreateProductRequest, Product>()
                .ForMember(dest => dest.Id, opt => opt.Ignore());

            CreateMap<UpdateProductRequest, Product>()
                .ForMember(dest => dest.Id, opt => opt.Ignore());


            CreateMap<Store, StoreDto>();

        }
    }
}
