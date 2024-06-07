using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace FliggyWebApi.Dto
{
    [XmlRoot("BookRQ")]
    public class BookRequest
    {
        public AuthenticationToken AuthenticationToken { get; set; }
        public string? TaoBaoOrderId { get; set; }
        public string? TaoBaoHotelId { get; set; }
        public string? HotelId { get; set; }
        public string? TaoBaoRoomTypeId { get; set; }
        public string? RoomTypeId { get; set; }
        public string? TaoBaoRatePlanId { get; set; }
        public string? RatePlanCode { get; set; }
        public string? Channel { get; set; }
        public string? TaoBaoGid { get; set; }
        public string? CheckIn { get; set; }
        public string? CheckOut { get; set; }
        public int? HourRent { get; set; }
        public string? EarliestArriveTime { get; set; }
        public string? LatestArriveTime { get; set; }
        public int? RoomNum { get; set; }
        public int? Occupancy { get; set; }
        public int? PriceType { get; set; }
        public int? IsMorningBuy { get; set; }
        public int? InventoryType { get; set; }
        public decimal? TotalPrice { get; set; }
        public decimal? OtherFee { get; set; }
        public decimal? PaidPrice { get; set; }
        public decimal? TotalSellerPromotion { get; set; }
        public GTInfo GTInfo { get; set; }
        public string? Currency { get; set; }
        public int? PaymentType { get; set; }
        public string? ContactName { get; set; }
        public string? ContactTel { get; set; }
        public string? ContactEmail { get; set; }
        public DailyInfos DailyInfos { get; set; }
        public OriDailyInfos OriDailyInfos { get; set; }
        public DisDailyInfos DisDailyInfos { get; set; }
        public TravelInfo TravelInfo { get; set; }
        public OrderGuests OrderGuests { get; set; }
        public string? Comment { get; set; }
        public int? GuaranteeType { get; set; }
        public MemberInfo MemberInfo { get; set; }
        public string? AlipayTradeNo { get; set; }
        public VoucherInfos VoucherInfos { get; set; }
        public CreditCardInfo CreditCardInfo { get; set; }
        public InvoiceInfo InvoiceInfo { get; set; }
        public PackageInfos PackageInfos { get; set; }
        public int? OriginPaymentType { get; set; }
        public string? HourRoomArriveTime { get; set; }
        public string? HourRoomLeaveTime { get; set; }
        public decimal? OnlineBookingItemSellerPromotion { get; set; }
        public string? Extensions { get; set; }
    }

    public class GTInfo
    {
        public decimal? GTPrice { get; set; }
        public string? GTStartTime { get; set; }
        public int? GTType { get; set; }
    }

    [XmlRoot("DailyInfos")]
    public class DailyInfos
    {
        [XmlElement("DailyInfo")]
        public List<DailyInfo> DailyInfoList { get; set; }
    }

    public class DailyInfo
    {
        public string? Day { get; set; }
        public decimal? Price { get; set; }
        public int? BreakFast { get; set; }
        public string? GiftInfo { get; set; }
    }

    [XmlRoot("OriDailyInfos")]
    public class OriDailyInfos
    {
        [XmlElement("DailyInfo")]
        public List<DailyInfo> OriDailyInfoList { get; set; }
    }

    [XmlRoot("DisDailyInfos")]
    public class DisDailyInfos
    {
        [XmlElement("DailyInfo")]
        public List<DailyInfo> DisDailyInfoList { get; set; }
    }

    public class TravelInfo
    {
        public int? OrderType { get; set; }
        public string? Company { get; set; }
    }

    [XmlRoot("OrderGuests")]
    public class OrderGuests
    {
        [XmlElement("OrderGuest")]
        public List<OrderGuest> OrderGuestList { get; set; }
    }

    public class OrderGuest
    {
        public string? Name { get; set; }
        public int? RoomPos { get; set; }
        public int? PersonType { get; set; } // 1 - 成人, 2 - 儿童
        public int? IdType { get; set; }
        public string? IdCode { get; set; }
        public int? Age { get; set; }
    }

    public class MemberInfo
    {
        public string? MemberName { get; set; }
        public string? MemberFliggyNum { get; set; }
        public string? MemberOutNum { get; set; }
        public string? MemberPhone { get; set; }
        public string? MemberEmail { get; set; }
        public int? MemberFliggyLevel { get; set; }
        public int? MemberOutLevel { get; set; }
        public string? MemberIdCard { get; set; }
        public string? BookMemberName { get; set; }
        public string? BookMemberFliggyNum { get; set; }
        public string? BookMemberOutNum { get; set; }
        public string? BookMemberPhone { get; set; }
        public string? BookMemberEmail { get; set; }
        public int? BookMemberFliggyLevel { get; set; }
        public int? BookMemberOutLevel { get; set; }
    }

    [XmlRoot("VoucherInfos")]
    public class VoucherInfos
    {
        [XmlElement("VoucherInfo")]
        public List<VoucherInfo> VoucherInfoList { get; set; }
    }

    public class VoucherInfo
    {
        public decimal? VoucherPomotionAmt { get; set; }
        public string? VoucherPomotionDesc { get; set; }
        public string? VoucherRuleDesc { get; set; }
        public string? VoucherOutCode { get; set; }
        public string? VoucherTid { get; set; }
        public string? VoucherId { get; set; }
        public int? VoucherNum { get; set; }
        public decimal? PaidFee { get; set; }
    }

    public class CreditCardInfo
    {
        public string? CardCode { get; set; }
        public string? CardHolderName { get; set; }
        public string? ExpirationDate { get; set; }
        public string? CardNumber { get; set; }
        public string? CvvCode { get; set; }
        public string? VccCreditCurrencyCode { get; set; }
        public decimal? VccCreditAmount { get; set; }
    }

    public class InvoiceInfo
    {
        public int? NeedInvoice { get; set; }
        public int? EarllyPrepare { get; set; }
        public string? SubmitTime { get; set; }
        public string? WantTime { get; set; }
        public int? PostType { get; set; }
        public int? InvoiceType { get; set; }
        public string? Comment { get; set; }
        public string? InvoiceTitle { get; set; }
        public string? CompanyTel { get; set; }
        public string? CompanyTax { get; set; }
        public string? RegisterAddress { get; set; }
        public string? Bank { get; set; }
        public string? BankAccount { get; set; }
        public string? ReceiverAddress { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverMobile { get; set; }
    }

    [XmlRoot("PackageInfos")]
    public class PackageInfos
    {
        public string? Desp { get; set; }

        [XmlElement("PackageInfo")]
        public List<PackageInfo> PackageInfoList { get; set; }
    }

    public class PackageInfo
    {
        public string? DimensionType { get; set; }
        public int? Quantity { get; set; }
        public string? Unit { get; set; }
        public int? PackageType { get; set; }
        public string? Name { get; set; }
        public string? SubDesp { get; set; }
        public string? Code { get; set; }
    }
}