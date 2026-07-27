namespace Domain.Enums;

public enum NotificationType
{
    OrderUpdate,
    OrderStatusChanged,
    PaymentReceived,
    PaymentFailed,
    BankTransferSubmitted,
    BankTransferReviewed,
    SupportMessage,
    ReviewResponse,
    PromotionalOffer,
    AccountUpdate,
    SystemAlert,
    NewReview,
    ChatMessage
}
