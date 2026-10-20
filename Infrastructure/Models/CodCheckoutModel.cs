using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Models;

public sealed class CodCheckoutModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự.")]
    public string Name { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(30)]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email nhận xác nhận.")]
    [StringLength(320)]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ nhận hàng khi đặt COD.")]
    [StringLength(1000, ErrorMessage = "Địa chỉ tối đa 1.000 ký tự.")]
    public string Address { get; set; } = "";
    [StringLength(2000, ErrorMessage = "Ghi chú tối đa 2.000 ký tự.")]
    public string? Note { get; set; }
}

public sealed class CodConfirmationModel
{
    public double Total { get; set; }
    public string Email { get; set; } = "";
}
