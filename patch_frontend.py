import re

with open('frontend/app.js', 'r', encoding='utf-8') as f:
    content = f.read()

# Add getGuestId helper
if 'function getGuestId()' not in content:
    content = content.replace('let _currentDetailProductId = null;', '''let _currentDetailProductId = null;
function getGuestId() {
    let gid = localStorage.getItem('guestId');
    if (!gid) {
        gid = 'guest_' + Math.random().toString(36).substr(2, 9);
        localStorage.setItem('guestId', gid);
    }
    return gid;
}''')

# Update toggleWishlist
content = re.sub(
    r"async function toggleWishlist\(productId\) \{\s*if \(\!currentToken\) \{\s*showToast\('info', '💡 Please log in to save favourites'\);\s*return;\s*\}",
    r"async function toggleWishlist(productId) {",
    content
)
content = re.sub(
    r"const resp = await fetch\(`\$\{currentApiBase\}/wishlist/\$\{productId\}`\, \{\s*method,\s*headers\: \{\s*'Authorization'\: `Bearer \$\{currentToken\}`\s*\}\s*\}\);",
    r"const resp = await fetch(`${currentApiBase}/wishlist/${productId}`, {\n            method,\n            headers: { 'Authorization': `Bearer ${currentToken}`, 'X-Guest-Id': getGuestId() }\n        });",
    content
)

# Update loadWishlistIds
content = re.sub(
    r"async function loadWishlistIds\(\) \{\s*if \(\!currentToken\) return;",
    r"async function loadWishlistIds() {",
    content
)
content = re.sub(
    r"const items = await fetch\(`\$\{currentApiBase\}/wishlist`\, \{\s*headers\: \{\s*'Authorization'\: `Bearer \$\{currentToken\}`\s*\}\s*\}\)\.then",
    r"const items = await fetch(`${currentApiBase}/wishlist`, {\n            headers: { 'Authorization': `Bearer ${currentToken}`, 'X-Guest-Id': getGuestId() }\n        }).then",
    content
)

# Update loadMyWishlist
content = re.sub(
    r"if \(\!currentToken\) \{\s*container\.innerHTML = `[\s\S]*?<\/div>`;\s*return;\s*\}",
    r"",
    content
)

# Update submitProductReview
content = re.sub(
    r"async function submitProductReview\(\) \{\s*if \(\!currentToken\) \{\s*showToast\('error', 'يجب تسجيل الدخول أولاً'\);\s*return;\s*\}",
    r"async function submitProductReview() {",
    content
)
content = re.sub(
    r"await fetch\(`\$\{currentApiBase\}/products/\$\{\_currentDetailProductId\}/reviews`, \{[\s\S]*?body: JSON\.stringify\(\{ rating: _pdRatingSelected, comment \}\)\s*\}\);",
    r"""const guestName = currentToken ? null : prompt('ما هو اسمك؟ (لإضافة التقييم كزائر)');
        if (!currentToken && !guestName) return;

        await fetch(`${currentApiBase}/reviews`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${currentToken}`, 'X-Guest-Id': getGuestId() },
            body: JSON.stringify({ 
                productId: _currentDetailProductId,
                rating: _pdRatingSelected, 
                comment: comment,
                guestName: guestName
            })
        });""",
    content
)

# Show review form always
content = re.sub(
    r"if \(reviewForm\) \{\s*if \(currentToken\) \{\s*reviewForm\.style\.display = 'block';\s*if \(loginMsg\) loginMsg\.style\.display = 'none';\s*\} else \{\s*reviewForm\.style\.display = 'none';\s*if \(loginMsg\) \{ loginMsg\.style\.display = 'block'; loginMsg\.parentElement\.style\.display = 'block'; \}\s*\}\s*\}",
    r"if (reviewForm) { reviewForm.style.display = 'block'; if (loginMsg) loginMsg.style.display = 'none'; }",
    content
)

with open('frontend/app.js', 'w', encoding='utf-8') as f:
    f.write(content)

print("PATCHED")
