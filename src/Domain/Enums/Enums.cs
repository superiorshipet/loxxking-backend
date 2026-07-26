namespace Domain.Enums;
public enum UserRole { Admin, StoreManager, SalesEmployee, Customer }
public enum OrderStatus { NewOrder, PendingApproval, Prepared, Shipping, Delivered, Cancelled, Incomplete }
public enum PaymentMethod { Cash, BankTransfer }
public enum ReviewStatus { Visible, Hidden }
public enum NotificationType { OrderUpdate, SiteVisit, NewReview, ChatMessage }
public enum BankTransferStatus { Pending, Confirmed, Rejected }
