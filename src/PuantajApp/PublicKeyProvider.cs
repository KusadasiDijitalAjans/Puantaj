namespace PuantajApp;

internal static class PublicKeyProvider
{
    // ÜRETİM (PRODUCTION) açık anahtarı — 2026-07-22 tarihinde üretilen RSA-4096 anahtar
    // çiftinin açık kısmıdır. Eşleşen özel anahtar yalnızca "secrets/production.private.pem"
    // içinde saklanır (git dışı, 600 izinli) ve yalnızca PuantajLicenseGenerator tarafından,
    // yalnızca lisans üreten kişide kullanılır. Özel anahtar hiçbir zaman bu uygulamaya veya
    // Setup paketine dahil edilmemelidir. Geliştirme anahtarı ("secrets/development.private.pem"
    // ile eşleşen eski açık anahtar) artık bu sabitte KULLANILMIYOR; yalnızca yerel geliştirme/
    // test amaçlı olarak secrets/ klasöründe kalmaya devam eder.
    public const string Pem = """
        -----BEGIN PUBLIC KEY-----
        MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEAzEzbG0P5AuJkvWXC907u
        dh4g80KM6W0eNJdH41j8BffnLxeE9Jkh2w7/GIhoWCFiPs4tDr+cWRbqXHl4E8uu
        Pfip9bQsbs3fU2Z+SwwBNLXCv3dr4DQd7OcZg56nJ5WXr7fqqZfEQx36Q5RyZlZu
        ZgZkpyl5yh09CKwMvD0etX+Rx0ftYLsbOkhhc9JNb72xKK+gxwazX0cg5iZbRsUj
        Qk0SkLa2JyXsEaQfEAjsNwKPrnCVHZmUMfQ+Iv2F+1bYjuaiO9opjBwaYtbb+99d
        XncbVHr9dVEZ0c7yuAiTsbSSdNDYnpJiT8R4LBMvn2HhC1LRBKIH2O/uO/3hnXJq
        9+UgoABS30JO4HecLk7/hnsBUbuxFHOake5qZlBEVcLOEiKsjyVcrQYhN9s1GyqD
        Xfpql7z8sAzNe+Itfv1zAMwwOzPKD9KNvc3ybLKAViS28IBosFjEdDKxQ12upPKF
        aUiwA8E9yI2TfXg4Zku5xrMtSTgMbxigOYhGN1A8bZzJV7rLJs1dlh4jvymYEAfn
        AU1uHJFVkBeSyI9bfFvLwFiazJahBq8ceHMqN+y+eX4/VzVmt2wWOS4ukxNHQ42M
        sS6e/d41uD1YjMS1yfxhK/uTU53u3IxnOMYr+D8yNXdz5Aoh0XD+AgxU0KXbQrkR
        SwKxEZy4iqcnFH3YsCse06MCAwEAAQ==
        -----END PUBLIC KEY-----
        """;
}
