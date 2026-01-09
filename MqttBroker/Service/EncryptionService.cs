using System.Security.Cryptography;
using System.Text;

namespace MqttBroker.Service
{
    internal class EncryptionService
    {
        private const int SaltSize = 16; // 128 bit salt for key derivation
        private const int KeySize = 32;  // 256 bit key for AES
        private const int Iterations = 100_000; // PBKDF2 iterations for key stretching
        private const int IvSize = 16;   // 128 bit IV for AES

        // Application-wide secret used for key derivation.
        // In production, store this securely (e.g., environment variable, secret manager).
        private static readonly string EncryptionSecret = "njfkdofh4789htfud7834583465789hdig!3627843gfbid73y!fuidsghfiudghsi!";

        public EncryptionService()
        {
        }

        /// <summary>
        /// Encrypts the given plain text using AES encryption with a key derived from a secret and a random salt.
        /// The output is a base64 string containing the salt, IV, and ciphertext.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <returns>Base64-encoded string containing salt + IV + ciphertext.</returns>
        public string Encrypt(string plainText)
        {
            // Generate a random salt for this encryption operation.
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            // Derive a cryptographic key from the secret and salt using PBKDF2.
            using var keyDerivation = new Rfc2898DeriveBytes(
                EncryptionSecret, salt, Iterations, HashAlgorithmName.SHA256);
            var key = keyDerivation.GetBytes(KeySize);

            // Create a new AES instance and generate a random IV.
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            var iv = aes.IV;

            // Create an encryptor and a memory stream to hold the encrypted data.
            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            using var ms = new MemoryStream();
            // Use a CryptoStream to perform the encryption and write the plaintext.
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs, Encoding.UTF8))
            {
                sw.Write(plainText);
            }

            // Get the encrypted bytes from the memory stream.
            var encryptedBytes = ms.ToArray();

            // Combine salt + IV + ciphertext into a single byte array for storage/transmission.
            var result = new byte[SaltSize + IvSize + encryptedBytes.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(iv, 0, result, SaltSize, IvSize);
            Buffer.BlockCopy(encryptedBytes, 0, result, SaltSize + IvSize, encryptedBytes.Length);

            // Return the combined data as a base64-encoded string.
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Decrypts a base64-encoded string produced by Encrypt, extracting the salt and IV,
        /// and using them to derive the key and decrypt the ciphertext.
        /// </summary>
        /// <param name="cipherText">Base64-encoded string containing salt + IV + ciphertext.</param>
        /// <returns>The decrypted plain text.</returns>
        public string Decrypt(string cipherText)
        {
            // Decode the base64 string to get the combined byte array.
            var fullCipher = Convert.FromBase64String(cipherText);

            // Extract the salt, IV, and ciphertext from the combined array.
            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            var cipherBytes = new byte[fullCipher.Length - SaltSize - IvSize];

            Buffer.BlockCopy(fullCipher, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(fullCipher, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(fullCipher, SaltSize + IvSize, cipherBytes, 0, cipherBytes.Length);

            // Derive the cryptographic key from the secret and extracted salt.
            using var keyDerivation = new Rfc2898DeriveBytes(
                EncryptionSecret, salt, Iterations, HashAlgorithmName.SHA256);
            var key = keyDerivation.GetBytes(KeySize);

            // Create a new AES instance and set the key and IV.
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            // Create a decryptor and a memory stream for the ciphertext.
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(cipherBytes);
            // Use a CryptoStream to perform the decryption and read the plaintext.
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }
    }
}
